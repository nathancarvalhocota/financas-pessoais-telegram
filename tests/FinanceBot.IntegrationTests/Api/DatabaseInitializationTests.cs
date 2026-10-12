using FinanceBot.Api.Configuration;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.IntegrationTests.Support;
using System.Threading.Channels;
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
    [InlineData(null)]
    [InlineData("28P01")]
    [InlineData("42601")]
    [InlineData("08006")]
    public async Task RelationalDatabase_InvokesMigrateOnceAndLogsOutcome(string? sqlState)
    {
        bool fail = sqlState is not null;
        Exception? failure = fail ? new PostgresException(PrivateData, "FATAL", "FATAL", sqlState!, detail: PrivateData) : null;
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
            Assert.Equal(sqlState, result.Properties["SqlState"]);
            Assert.DoesNotContain(logs.Events, item => item.EventId.Name == "DatabaseInitializationCompleted");
        }
        else Assert.Equal("completed", result.Properties["Outcome"]);
        AssertDurationAndPrivacy(logs, result);
        Assert.Equal(2, logs.Events.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task DatabaseStartingUp_RetriesWithBackoffAndFreshContextsThenCompletes(int failures)
    {
        using var fixture = new StartupFixture(call => call <= failures ? StartingUp() : null);
        Task startup = fixture.StartAsync();

        Assert.False(startup.IsCompleted);
        for (int attempt = 1; attempt <= failures; attempt++)
        {
            ControlledTimer timer = await fixture.Clock.NextDelayAsync();
            Assert.Equal(TimeSpan.FromSeconds(1 << attempt), timer.DueTime);
            Assert.Equal(attempt, fixture.Migrator.Calls);
            AssertDisposed(fixture.Contexts[attempt - 1]);
            timer.Fire();
        }
        await startup.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(failures + 1, fixture.Migrator.Calls);
        Assert.Equal(failures + 1, fixture.Contexts.Count);
        Assert.All(fixture.Contexts, AssertDisposed);
        Assert.Equal(fixture.Contexts.Count, fixture.Contexts.Distinct().Count());
        Assert.Null(fixture.Migrator.TargetMigration);
        AssertStarted(fixture.Logs);
        AssertRetries(fixture.Logs, failures);
        CapturedLog completed = Assert.Single(fixture.Logs.Events, item => item.EventId.Name == "DatabaseInitializationCompleted");
        Assert.Equal("migrate", completed.Properties["Stage"]);
        Assert.Equal("completed", completed.Properties["Outcome"]);
        Assert.DoesNotContain(fixture.Logs.Events, item => item.Level >= LogLevel.Warning);
        AssertDurationAndPrivacy(fixture.Logs, completed);
    }

    [Fact]
    public async Task DatabaseStillStartingUp_AfterFourAttemptsLogsOneFailureAndPropagates()
    {
        PostgresException failure = StartingUp();
        using var fixture = new StartupFixture(_ => failure);
        Task startup = fixture.StartAsync();

        Assert.False(startup.IsCompleted);
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            ControlledTimer timer = await fixture.Clock.NextDelayAsync();
            Assert.Equal(TimeSpan.FromSeconds(1 << attempt), timer.DueTime);
            timer.Fire();
        }
        PostgresException actual = await Assert.ThrowsAsync<PostgresException>(() => startup.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Same(failure, actual);
        Assert.Equal(4, fixture.Migrator.Calls);
        Assert.All(fixture.Contexts, AssertDisposed);
        AssertRetries(fixture.Logs, 3);
        CapturedLog failed = Assert.Single(fixture.Logs.Events, item => item.EventId.Name == "DatabaseInitializationFailed");
        Assert.Equal(LogLevel.Error, failed.Level);
        Assert.Equal("57P03", failed.Properties["SqlState"]);
        Assert.DoesNotContain(fixture.Logs.Events, item => item.EventId.Name == "DatabaseInitializationCompleted");
        AssertDurationAndPrivacy(fixture.Logs, failed);
    }

    [Fact]
    public async Task PermanentFailureAfterTransientFailure_StopsWithoutFurtherRetries()
    {
        var failure = new PostgresException(PrivateData, "ERROR", "ERROR", "42601");
        using var fixture = new StartupFixture(call => call == 1 ? StartingUp() : failure);
        Task startup = fixture.StartAsync();
        Assert.False(startup.IsCompleted);
        (await fixture.Clock.NextDelayAsync()).Fire();

        Assert.Same(failure, await Assert.ThrowsAsync<PostgresException>(() => startup.WaitAsync(TimeSpan.FromSeconds(5))));
        Assert.Equal(2, fixture.Migrator.Calls);
        AssertRetries(fixture.Logs, 1);
        CapturedLog failed = Assert.Single(fixture.Logs.Events, item => item.EventId.Name == "DatabaseInitializationFailed");
        Assert.Equal("42601", failed.Properties["SqlState"]);
        AssertDurationAndPrivacy(fixture.Logs, failed);
    }

    [Fact]
    public async Task CancellationDuringBackoff_StopsWithoutAnotherMigrationAttempt()
    {
        using var fixture = new StartupFixture(_ => StartingUp());
        using var cancellation = new CancellationTokenSource();
        Task startup = fixture.StartAsync(cancellation.Token);
        Assert.False(startup.IsCompleted);
        ControlledTimer timer = await fixture.Clock.NextDelayAsync();

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => startup.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(1, fixture.Migrator.Calls);
        Assert.Equal(cancellation.Token, fixture.Migrator.Token);
        Assert.True(timer.IsDisposed);
        timer.Fire();
        Assert.Equal(1, fixture.Migrator.Calls);
        AssertDisposed(Assert.Single(fixture.Contexts));
        Assert.DoesNotContain(fixture.Logs.Events, item => item.EventId.Name == "DatabaseInitializationCompleted");
        CapturedLog failed = Assert.Single(fixture.Logs.Events, item => item.EventId.Name == "DatabaseInitializationFailed");
        AssertDurationAndPrivacy(fixture.Logs, failed);
    }

    [Fact]
    public async Task CancellationBeforeStartup_DoesNotResolveContextOrAttemptMigration()
    {
        using var fixture = new StartupFixture(_ => null);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.StartAsync(cancellation.Token));

        Assert.Equal(0, fixture.Migrator.Calls);
        Assert.Empty(fixture.Contexts);
        Assert.DoesNotContain(fixture.Logs.Events, item => item.EventId.Name == "DatabaseInitializationRetrying");
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

    private static PostgresException StartingUp() => new(PrivateData, "FATAL", "FATAL", "57P03", detail: PrivateData);

    private static void AssertDisposed(FinanceBotDbContext context) =>
        Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker);

    private static void AssertRetries(CapturingLoggerProvider logs, int count)
    {
        CapturedLog[] retries = logs.Events.Where(item => item.EventId.Name == "DatabaseInitializationRetrying").ToArray();
        Assert.Equal(count, retries.Length);
        for (int index = 0; index < retries.Length; index++)
        {
            CapturedLog retry = retries[index];
            Assert.Equal(1103, retry.EventId.Id);
            Assert.Equal(LogLevel.Information, retry.Level);
            Assert.Equal("migrate", retry.Properties["Stage"]);
            Assert.Equal("57P03", retry.Properties["SqlState"]);
            Assert.Equal(index + 1, retry.Properties["Attempt"]);
            Assert.Equal(4, retry.Properties["MaxAttempts"]);
            Assert.Equal((double)(1 << (index + 1)), retry.Properties["DelaySeconds"]);
        }
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

    private sealed class StartupFixture : IDisposable
    {
        private readonly ServiceProvider _efProvider;
        private readonly ServiceProvider _provider;
        public RecordingMigrator Migrator { get; }
        public ControlledTimeProvider Clock { get; } = new();
        public CapturingLoggerProvider Logs { get; } = new();
        public List<FinanceBotDbContext> Contexts { get; } = new();

        public StartupFixture(Func<int, Exception?> failureByCall)
        {
            Migrator = new RecordingMigrator(null) { FailureByCall = failureByCall };
            var efServices = new ServiceCollection();
            efServices.AddEntityFrameworkNpgsql();
            efServices.AddScoped<IMigrator>(_ => Migrator);
            _efProvider = efServices.BuildServiceProvider();
            var options = new DbContextOptionsBuilder<FinanceBotDbContext>()
                .UseNpgsql("Host=PRIVATE_HOST;Password=PRIVATE_DATABASE_PASSWORD")
                .UseInternalServiceProvider(_efProvider).Options;
            var services = new ServiceCollection();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddScoped(_ =>
            {
                var context = new FinanceBotDbContext(options);
                Contexts.Add(context);
                return context;
            });
            _provider = services.BuildServiceProvider();
        }

        public Task StartAsync(CancellationToken cancellationToken = default) =>
            _provider.InitializeDatabaseAsync(Logs.CreateLogger("Startup"), cancellationToken);

        public void Dispose()
        {
            _provider.Dispose();
            _efProvider.Dispose();
        }
    }

    private sealed class ControlledTimeProvider : TimeProvider
    {
        private readonly Channel<ControlledTimer> _delays = Channel.CreateUnbounded<ControlledTimer>();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ControlledTimer(callback, state, dueTime);
            _delays.Writer.TryWrite(timer);
            return timer;
        }

        public Task<ControlledTimer> NextDelayAsync() =>
            _delays.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    private sealed class ControlledTimer(TimerCallback callback, object? state, TimeSpan dueTime) : ITimer
    {
        public TimeSpan DueTime { get; } = dueTime;
        public bool IsDisposed { get; private set; }
        public void Fire()
        {
            if (!IsDisposed) callback(state);
        }
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
        public void Dispose() => IsDisposed = true;
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingMigrator(Exception? failure) : IMigrator
    {
        public Func<int, Exception?>? FailureByCall { get; init; }
        public int Calls { get; private set; }
        public string? TargetMigration { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task MigrateAsync(string? targetMigration = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            TargetMigration = targetMigration;
            Token = cancellationToken;
            Exception? currentFailure = FailureByCall is null ? failure : FailureByCall(Calls);
            return currentFailure is null ? Task.CompletedTask : Task.FromException(currentFailure);
        }
        public void Migrate(string? targetMigration = null) => throw new NotSupportedException();
        public string GenerateScript(string? fromMigration = null, string? toMigration = null,
            MigrationsSqlGenerationOptions options = MigrationsSqlGenerationOptions.Default) => throw new NotSupportedException();
    }
}
