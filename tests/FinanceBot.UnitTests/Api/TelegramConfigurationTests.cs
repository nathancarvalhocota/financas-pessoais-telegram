using System.Net;
using FinanceBot.Api.Configuration;
using FinanceBot.Api.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceBot.UnitTests.Api;

public sealed class TelegramConfigurationTests
{
    private const string Token = "FAKE_BOT_TOKEN_IDENTIFIABLE";
    private const string Secret = "FAKE_WEBHOOK_SECRET_IDENTIFIABLE";
    private const string Message = "FAKE_FINANCIAL_MESSAGE_IDENTIFIABLE";

    [Theory]
    [InlineData("", "valid-secret", true, "Telegram:BotToken")]
    [InlineData("  ", "valid-secret", true, "Telegram:BotToken")]
    [InlineData(Token, "", true, "Telegram:WebhookSecret")]
    [InlineData(Token, "invalid secret", true, "Telegram:WebhookSecret")]
    [InlineData(Token, "inválido", true, "Telegram:WebhookSecret")]
    [InlineData(Token, "secret\n", true, "Telegram:WebhookSecret")]
    [InlineData(Token, "secret!", true, "Telegram:WebhookSecret")]
    [InlineData(Token, Token, true, "Telegram:WebhookSecret")]
    [InlineData(Token, Secret, false, "Telegram:AllowedChatIds")]
    public void Options_WithInvalidConfiguration_RejectKeysWithoutValues(
        string token, string secret, bool hasChat, string expectedKey)
    {
        using ServiceProvider provider = CreateServices(token, secret, hasChat).BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<TelegramOptions>>().Value);

        Assert.Contains(expectedKey, exception.Message);
        Assert.DoesNotContain(Token, exception.ToString());
        Assert.DoesNotContain(Secret, exception.ToString());
        if (!string.IsNullOrWhiteSpace(secret))
        {
            Assert.DoesNotContain(secret, exception.ToString());
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(256, true)]
    [InlineData(257, false)]
    public void Options_EnforceSecretLength(int length, bool valid)
    {
        using ServiceProvider provider = CreateServices(Token, new string('a', length), true)
            .BuildServiceProvider();

        if (valid)
            Assert.Equal(length, provider.GetRequiredService<IOptions<TelegramOptions>>().Value.WebhookSecret.Length);
        else
            Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TelegramOptions>>().Value);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("http-error")]
    [InlineData("transport-error")]
    public async Task ProductionHttpClient_AtTrace_DoesNotLogCredentialsOrPayloads(string scenario)
    {
        CapturingLoggerProvider logs = new();
        IServiceCollection services = CreateServices(Token, Secret, true);
        services.AddLogging(logging => logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddHttpClient<TelegramMessageSender>().ConfigurePrimaryHttpMessageHandler(() =>
            new FakeTelegramHandler(scenario));
        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<ILoggerFactory>().CreateLogger("Probe").LogTrace("Trace enabled");
        TelegramMessageSender sender = provider.GetRequiredService<TelegramMessageSender>();

        Exception? exception = await Record.ExceptionAsync(() => sender.SendMessageAsync(123, Message, CancellationToken.None));

        if (scenario == "transport-error") Assert.IsType<HttpRequestException>(exception);
        else Assert.Null(exception);

        string captured = string.Join("\n", logs.Entries);
        Assert.Contains("Trace enabled", captured);
        foreach (string sensitive in new[] { Token, Secret, Message, "api.telegram.org", "RAW_RESPONSE_IDENTIFIABLE", "INNER_EXCEPTION_IDENTIFIABLE" })
            Assert.DoesNotContain(sensitive, captured);
        Assert.DoesNotContain("System.Net.Http.HttpClient", captured);
        if (scenario == "http-error") Assert.Contains("403", captured);
        if (scenario == "transport-error") Assert.Contains("HttpRequestException", captured);
    }

    private static IServiceCollection CreateServices(string token, string secret, bool hasChat)
    {
        Dictionary<string, string?> values = new()
        {
            ["Telegram:BotToken"] = token,
            ["Telegram:WebhookSecret"] = secret
        };
        if (hasChat) values["Telegram:AllowedChatIds:0"] = "123";
        ServiceCollection services = new();
        services.AddTelegramMessaging(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        return services;
    }

    private sealed class FakeTelegramHandler(string scenario) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (scenario == "transport-error")
                throw new HttpRequestException($"{request.RequestUri} {Secret} {Message}",
                    new InvalidOperationException($"INNER_EXCEPTION_IDENTIFIABLE {Token}"));
            return Task.FromResult(new HttpResponseMessage(scenario == "success" ? HttpStatusCode.OK : HttpStatusCode.Forbidden)
            {
                Content = new StringContent($"RAW_RESPONSE_IDENTIFIABLE {Token} {Secret} {Message}")
            });
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(string category, List<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => entries.Add($"{category}: {formatter(state, exception)} {exception}");
    }
}
