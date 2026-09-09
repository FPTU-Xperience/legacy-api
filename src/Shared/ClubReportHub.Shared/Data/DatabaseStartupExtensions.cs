using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubReportHub.Shared.Data;

public static class DatabaseStartupExtensions
{
    private const int MaxAttempts = 6;

    public static async Task ApplyMigrationsWithRetryAsync(
        this DbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await RunWithRetryAsync(
            db,
            () => db.Database.MigrateAsync(cancellationToken),
            "apply migrations",
            logger,
            cancellationToken);
    }

    public static async Task EnsureCreatedWithRetryAsync(
        this DbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await RunWithRetryAsync(
            db,
            () => db.Database.EnsureCreatedAsync(cancellationToken),
            "ensure database exists",
            logger,
            cancellationToken);
    }

    private static async Task RunWithRetryAsync(
        DbContext db,
        Func<Task> operation,
        string operationName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (SqlException ex) when (IsDatabaseAlreadyExists(ex))
            {
                if (await CanConnectAsync(db, cancellationToken))
                {
                    logger.LogWarning(ex, "Database or object already exists and is reachable; continuing service startup.");
                    return;
                }

                if (attempt < MaxAttempts)
                {
                    logger.LogWarning(ex, "Database or object already exists while trying to {OperationName}. Retrying startup database check.", operationName);
                    await WaitBeforeRetryAsync(attempt, cancellationToken);
                    continue;
                }

                throw new InvalidOperationException($"Failed to {operationName} after {MaxAttempts} attempts: database already exists but is not reachable.", ex);
            }
            catch (SqlException ex) when (IsTransientStartupFailure(ex) && attempt < MaxAttempts)
            {
                logger.LogWarning(ex, "Transient SQL startup failure while trying to {OperationName}. Retry {Attempt}/{MaxAttempts}.", operationName, attempt, MaxAttempts);
                await WaitBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to {OperationName} on attempt {Attempt}/{MaxAttempts}.", operationName, attempt, MaxAttempts);
                if (attempt >= MaxAttempts)
                {
                    throw new InvalidOperationException($"Failed to {operationName} after {MaxAttempts} attempts.", ex);
                }
                await WaitBeforeRetryAsync(attempt, cancellationToken);
            }
        }

        // If we exit the loop normally, all retries failed
        throw new InvalidOperationException($"Failed to {operationName} after {MaxAttempts} attempts.");
    }

    private static Task WaitBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Min(10, attempt * 2));
        return Task.Delay(delay, cancellationToken);
    }

    private static bool IsDatabaseAlreadyExists(SqlException ex) =>
        ex.Errors.Cast<SqlError>().Any(error => error.Number == 1801 || error.Number == 2714);

    private static bool IsTransientStartupFailure(SqlException ex)
    {
        int[] transientErrorNumbers =
        [
            53,
            64,
            233,
            4060,
            10053,
            10054,
            10060,
            10061
        ];

        return ex.Errors.Cast<SqlError>().Any(error => transientErrorNumbers.Contains(error.Number));
    }

    private static async Task<bool> CanConnectAsync(DbContext db, CancellationToken cancellationToken)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken);
        }
        catch (SqlException)
        {
            return false;
        }
    }
}
