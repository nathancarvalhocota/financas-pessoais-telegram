using FinanceBot.Api.Commands;
using FinanceBot.Api.Configuration;
using FinanceBot.Api.Messaging;
using Microsoft.Extensions.Options;

namespace FinanceBot.Api.Endpoints;

public static class TelegramWebhookEndpoint
{
    public static IEndpointRouteBuilder MapTelegramWebhookEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/telegram/webhook", HandleWebhookAsync)
            .WithName("TelegramWebhook")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(
        TelegramWebhookRequest? update,
        HttpRequest request,
        TelegramCommandRouter telegramCommandRouter,
        TelegramMessageSender telegramMessageSender,
        IOptions<TelegramOptions> telegramOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("TelegramWebhook");

        if (!request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secrets)
            || secrets.Count != 1
            || !string.Equals(secrets[0], telegramOptions.Value.WebhookSecret, StringComparison.Ordinal))
        {
            logger.LogWarning("Telegram webhook authentication failed.");
            return Results.Unauthorized();
        }

        if (update?.Message is null)
        {
            logger.LogInformation("Ignoring update without message payload.");
            return Results.Ok();
        }

        if (string.IsNullOrWhiteSpace(update.Message.Text))
        {
            logger.LogInformation("Ignoring message without text content.");
            return Results.Ok();
        }

        if (update.Message.Chat is null || update.Message.Chat.Id == 0)
        {
            logger.LogWarning("Ignoring message without chat id in payload.");
            return Results.Ok();
        }

        long targetChatId = update.Message.Chat.Id;
        if (!telegramOptions.Value.AllowedChatIds.Contains(targetChatId))
        {
            logger.LogWarning("Ignoring message from unauthorized chat {ChatId}.", targetChatId);
            return Results.Ok();
        }

        string receivedText = update.Message.Text.Trim();
        DateTimeOffset messageDateUtc = ResolveMessageDateUtc(update.Message);

        string responseText = await telegramCommandRouter.RouteMessageAsync(
            receivedText,
            messageDateUtc,
            cancellationToken);

        try
        {
            await telegramMessageSender.SendMessageAsync(
                targetChatId,
                responseText,
                cancellationToken);
        }
        catch (Exception exception)
        {
            // Why: webhook endpoint should remain resilient and still acknowledge Telegram updates.
            logger.LogError(
                "Failed to send Telegram reply for chat {ChatId}. Stage: {Stage}; exception type: {ExceptionType}; status: {StatusCode}.",
                targetChatId,
                "sendMessage",
                exception.GetType().Name,
                exception is HttpRequestException httpException ? (int?)httpException.StatusCode : null);
        }

        return Results.Ok();
    }

    private static DateTimeOffset ResolveMessageDateUtc(TelegramMessageRequest messageRequest)
    {
        if (messageRequest.Date.HasValue)
        {
            return DateTimeOffset.FromUnixTimeSeconds(messageRequest.Date.Value);
        }

        return DateTimeOffset.UtcNow;
    }
}
