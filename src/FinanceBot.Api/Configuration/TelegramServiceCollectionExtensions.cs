using FinanceBot.Api.Messaging;

namespace FinanceBot.Api.Configuration;

public static class TelegramServiceCollectionExtensions
{
    public static IHttpClientBuilder AddTelegramMessaging(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BotToken),
                "Telegram:BotToken must be configured.")
            .Validate(options => IsValidWebhookSecret(options.WebhookSecret),
                "Telegram:WebhookSecret must contain 1 to 256 ASCII letters, digits, underscores or hyphens.")
            .Validate(options => options.WebhookSecret != options.BotToken,
                "Telegram:WebhookSecret must differ from Telegram:BotToken.")
            .Validate(options => options.AllowedChatIds is { Length: > 0 },
                "Telegram:AllowedChatIds must contain at least one chat.")
            .ValidateOnStart();

        // The token is part of the URI; header redaction cannot protect it.
        return services.AddHttpClient<TelegramMessageSender>().RemoveAllLoggers();
    }

    private static bool IsValidWebhookSecret(string? secret) =>
        secret is { Length: >= 1 and <= 256 } &&
        secret.All(character => character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
}
