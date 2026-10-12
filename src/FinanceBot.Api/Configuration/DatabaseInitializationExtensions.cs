using System.Diagnostics;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinanceBot.Api.Configuration;

public static class DatabaseInitializationExtensions
{
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
