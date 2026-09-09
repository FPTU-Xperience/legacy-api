namespace ReportService.Attachments;

public static class ReportAttachmentPolicy
{
    public static AttachmentValidationResult Validate(
        string? fileName,
        string? contentType,
        long sizeBytes,
        ReportAttachmentOptions options)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return AttachmentValidationResult.Fail("File name is required.");
        }

        if (sizeBytes <= 0)
        {
            return AttachmentValidationResult.Fail("File is empty.");
        }

        if (sizeBytes > options.MaxSizeBytes)
        {
            return AttachmentValidationResult.Fail($"Maximum attachment size is {options.MaxSizeBytes / 1024 / 1024} MB.");
        }

        if (string.IsNullOrWhiteSpace(contentType) ||
            !options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return AttachmentValidationResult.Fail("File type is not allowed.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) ||
            !options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return AttachmentValidationResult.Fail("File extension is not allowed.");
        }

        return AttachmentValidationResult.Success();
    }

    public static string GetSafeFileName(string fileName)
    {
        var normalizedName = fileName.Replace('\\', '/');
        var safeName = Path.GetFileName(normalizedName).Trim();
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "attachment";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = string.Concat(safeName.Select(character =>
            invalid.Contains(character) || character is '/' or '\\' || char.IsControl(character) ? '_' : character));

        if (cleaned.Length <= 160)
        {
            return cleaned;
        }

        var extension = Path.GetExtension(cleaned);
        var stem = Path.GetFileNameWithoutExtension(cleaned);
        var maxStemLength = Math.Max(1, 160 - extension.Length);
        return stem[..Math.Min(stem.Length, maxStemLength)] + extension;
    }

    public static string CreateStoredFileName(string safeFileName)
    {
        var extension = Path.GetExtension(safeFileName);
        return $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
    }

    public static string ResolveStorageRoot(string configuredPath, string contentRootPath)
    {
        var storagePath = string.IsNullOrWhiteSpace(configuredPath)
            ? "attachments"
            : configuredPath.Trim();

        var root = Path.IsPathRooted(storagePath)
            ? storagePath
            : Path.Combine(contentRootPath, storagePath);

        return Path.GetFullPath(root);
    }
}

public sealed record AttachmentValidationResult(bool Succeeded, string? ErrorMessage)
{
    public static AttachmentValidationResult Success() => new(true, null);
    public static AttachmentValidationResult Fail(string errorMessage) => new(false, errorMessage);
}
