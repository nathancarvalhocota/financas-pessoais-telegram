using System.Diagnostics;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceBot.Api.Configuration;

public static class DatabaseInitializationExtensions
{
    private const int MaxAttempts = 4;

    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        long started = Stopwatch.GetTimestamp();
        string stage = "resolve";
        logger.LogInformation(new EventId(1100, "DatabaseInitializationStarted"),
            "Database initialization started. Stage: {Stage}.", stage);
        try
        {
            TimeProvider timeProvider = services.GetService<TimeProvider>() ?? TimeProvider.System;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                stage = "resolve";
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using IServiceScope scope = services.CreateScope();
                    FinanceBotDbContext db = scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>();
                    stage = "inspect";
                    bool relational = db.Database.IsRelational();
                    if (relational)
                    {
                        stage = "migrate";
                        await db.Database.MigrateAsync(cancellationToken);
                    }

                    logger.LogInformation(new EventId(1101, "DatabaseInitializationCompleted"),
                        "Database initialization completed. Stage: {Stage}; outcome: {Outcome}; duration ms: {DurationMs}.",
                        stage, relational ? "completed" : "skipped", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                    return;
                }
                catch (PostgresException exception) when (
                    stage == "migrate" && exception.SqlState == PostgresErrorCodes.CannotConnectNow &&
                    attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
                {
                    TimeSpan delay = TimeSpan.FromSeconds(1 << attempt);
                    logger.LogInformation(new EventId(1103, "DatabaseInitializationRetrying"),
                        "Database temporarily unavailable. Stage: {Stage}; SQL state: {SqlState}; attempt: {Attempt}/{MaxAttempts}; retry delay seconds: {DelaySeconds}.",
                        stage, exception.SqlState, attempt, MaxAttempts, delay.TotalSeconds);
                    await Task.Delay(delay, timeProvider, cancellationToken);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(new EventId(1102, "DatabaseInitializationFailed"),
                "Database initialization failed. Stage: {Stage}; exception type: {ExceptionType}; SQL state: {SqlState}; duration ms: {DurationMs}.",
                stage, exception.GetType().Name,
                exception is PostgresException postgresException ? postgresException.SqlState : null,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }
}
