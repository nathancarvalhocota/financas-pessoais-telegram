using FinanceBot.Api.Configuration;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace FinanceBot.IntegrationTests.Api;

public sealed class DatabaseInitializationTests
{
    private const string PrivateData = "Host=PRIVATE_HOST;Password=PRIVATE_DATABASE_PASSWORD;PRIVATE_EXCEPTION";

    [Fact]
    public async Task NonRelationalDatabase_LogsStartAndSkippedCompletion()
    {
        ServiceCollection services = new();
        services.AddDbContext<FinanceBotDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        using ServiceProvider provider = services.BuildServiceProvider();
        CapturingLoggerProvider logs = new();

        await provider.InitializeDatabaseAsync(logs.CreateLogger("Startup"));

        AssertStarted(logs);
        CapturedLog completed = Assert.Single(logs.Events, item => item.EventId.Name == "DatabaseInitializationCompleted");
        Assert.Equal("inspect", completed.Properties["Stage"]);
        Assert.Equal("skipped", completed.Properties["Outcome"]);
        AssertDurationAndPrivacy(logs, completed);
        Assert.Equal(2, logs.Events.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RelationalDatabase_InvokesMigrateOnceAndLogsOutcome(bool fail)
    {
        Exception? failure = fail ? new PostgresException(PrivateData, "FATAL", "FATAL", "57P03", detail: PrivateData) : null;
        RecordingMigrator migrator = new(failure);
        ServiceCollection efServices = new();
        efServices.AddEntityFrameworkNpgsql();
        efServices.AddScoped<IMigrator>(_ => migrator);
        using ServiceProvider efProvider = efServices.BuildServiceProvider();
        ServiceCollection services = new();
        services.AddDbContext<FinanceBotDbContext>(options => options
            .UseNpgsql("Host=PRIVATE_HOST;Password=PRIVATE_DATABASE_PASSWORD")
            .UseInternalServiceProvider(efProvider));
        using ServiceProvider provider = services.BuildServiceProvider();
        CapturingLoggerProvider logs = new();
        using CancellationTokenSource cancellation = new();

        Exception? actual = await Record.ExceptionAsync(() => provider.InitializeDatabaseAsync(
            logs.CreateLogger("Startup"), cancellation.Token));

        Assert.Same(failure, actual);
        Assert.Equal(1, migrator.Calls);
        Assert.Null(migrator.TargetMigration);
        Assert.Equal(cancellation.Token, migrator.Token);
        AssertStarted(logs);
        CapturedLog result = Assert.Single(logs.Events, item => item.EventId.Name ==
            (fail ? "DatabaseInitializationFailed" : "DatabaseInitializationCompleted"));
        Assert.Equal("migrate", result.Properties["Stage"]);
        if (fail)
        {
            Assert.Equal(LogLevel.Error, result.Level);
            Assert.Equal("PostgresException", result.Properties["ExceptionType"]);
            Assert.Equal("57P03", result.Properties["SqlState"]);
            Assert.DoesNotContain(logs.Events, item => item.EventId.Name == "DatabaseInitializationCompleted");
        }
        else Assert.Equal("completed", result.Properties["Outcome"]);
        AssertDurationAndPrivacy(logs, result);
        Assert.Equal(2, logs.Events.Count);
    }

    [Theory]
    [InlineData("resolve")]
    [InlineData("inspect")]
    public async Task FailureBeforeMigration_LogsSafeStageAndPropagates(string stage)
    {
        ServiceCollection services = new();
        Exception? failure = stage == "resolve" ? new InvalidOperationException(PrivateData) : null;
        if (failure is not null)
            services.AddScoped<FinanceBotDbContext>(_ => throw failure);
        else
            services.AddScoped(_ => new FinanceBotDbContext(new DbContextOptions<FinanceBotDbContext>()));
        using ServiceProvider provider = services.BuildServiceProvider();
        CapturingLoggerProvider logs = new();

        Exception actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.InitializeDatabaseAsync(logs.CreateLogger("Startup")));

        if (failure is not null) Assert.Same(failure, actual);
        AssertStarted(logs);
        CapturedLog result = Assert.Single(logs.Events, item => item.EventId.Name == "DatabaseInitializationFailed");
        Assert.Equal(stage, result.Properties["Stage"]);
        Assert.Equal("InvalidOperationException", result.Properties["ExceptionType"]);
        Assert.Null(result.Properties["SqlState"]);
        AssertDurationAndPrivacy(logs, result);
        Assert.Equal(2, logs.Events.Count);
    }

    private static void AssertStarted(CapturingLoggerProvider logs)
    {
        CapturedLog first = logs.Events.First();
        Assert.Equal("DatabaseInitializationStarted", first.EventId.Name);
        Assert.Equal("resolve", first.Properties["Stage"]);
        Assert.Equal(LogLevel.Information, first.Level);
    }

    private static void AssertDurationAndPrivacy(CapturingLoggerProvider logs, CapturedLog result)
    {
        double duration = Assert.IsType<double>(result.Properties["DurationMs"]);
        Assert.True(double.IsFinite(duration) && duration >= 0);
        Assert.All(logs.Events, item => Assert.Null(item.Exception));
        string captured = string.Join("\n", logs.Entries);
        foreach (string marker in new[] { PrivateData, "PRIVATE_HOST", "PRIVATE_DATABASE_PASSWORD", "PRIVATE_EXCEPTION" })
            Assert.DoesNotContain(marker, captured);
    }

    private sealed class RecordingMigrator(Exception? failure) : IMigrator
    {
        public int Calls { get; private set; }
        public string? TargetMigration { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task MigrateAsync(string? targetMigration = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            TargetMigration = targetMigration;
            Token = cancellationToken;
            return failure is null ? Task.CompletedTask : Task.FromException(failure);
        }
        public void Migrate(string? targetMigration = null) => throw new NotSupportedException();
        public string GenerateScript(string? fromMigration = null, string? toMigration = null,
            MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default) => throw new NotSupportedException();
    }
}
