using AuthService.Contracts;

namespace AuthService.Validators;

public static class AuthValidators
{
    public static ValidationResult ValidateLogin(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return ValidationResult.Failure("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return ValidationResult.Failure("Password is required.");
        }

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateRegister(RegisterRequest request)
    {
        var errors = new List<string>();

        // Required fields
        if (string.IsNullOrWhiteSpace(request.Username))
            errors.Add("Username is required.");
        if (string.IsNullOrWhiteSpace(request.FullName))
            errors.Add("Full name is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            errors.Add("Password is required.");

        // Length validation
        if (request.Username?.Length > 100)
            errors.Add("Username must not exceed 100 characters.");
        if (request.FullName?.Length > 200)
            errors.Add("Full name must not exceed 200 characters.");
        if (request.Email?.Length > 255)
            errors.Add("Email must not exceed 255 characters.");

        // Email format
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out var parsedEmail)
                || !string.Equals(parsedEmail.Address, request.Email, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Email address is invalid.");
            }
        }

        // Password complexity
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 8)
            {
                errors.Add("Password must be at least 8 characters.");
            }
            else
            {
                var hasUpper = request.Password.Any(char.IsUpper);
                var hasLower = request.Password.Any(char.IsLower);
                var hasDigit = request.Password.Any(char.IsDigit);
                var hasSpecial = request.Password.Any(c => !char.IsLetterOrDigit(c));

                if (!(hasUpper && hasLower && hasDigit && hasSpecial))
                {
                    errors.Add("Password must contain uppercase, lowercase, digit, and special character.");
                }
            }
        }

        return errors.Count > 0
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    public static ValidationResult ValidateCreateUser(CreateUserRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Username))
            errors.Add("Username is required.");
        if (string.IsNullOrWhiteSpace(request.FullName))
            errors.Add("Full name is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add("Email is required.");
        if (string.IsNullOrWhiteSpace(request.Password))
            errors.Add("Password is required.");

        if (request.Username?.Length > 100)
            errors.Add("Username must not exceed 100 characters.");
        if (request.FullName?.Length > 200)
            errors.Add("Full name must not exceed 200 characters.");
        if (request.Email?.Length > 255)
            errors.Add("Email must not exceed 255 characters.");

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
            {
                errors.Add("Email address is invalid.");
            }
        }

        if (request.Password?.Length < 8)
            errors.Add("Password must be at least 8 characters.");

        return errors.Count > 0
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    public static ValidationResult ValidateUpdateUser(UpdateUserRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.FullName))
            errors.Add("Full name is required.");
        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add("Email is required.");

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            if (!System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
            {
                errors.Add("Email address is invalid.");
            }
        }

        return errors.Count > 0
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }
}

public class ValidationResult
{
    public bool IsValid { get; private set; }
    public IReadOnlyList<string> Errors { get; private set; } = [];

    private ValidationResult() { }

    public static ValidationResult Success() => new() { IsValid = true };

    public static ValidationResult Failure(string error) =>
        new() { IsValid = false, Errors = [error] };

    public static ValidationResult Failure(IEnumerable<string> errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}
