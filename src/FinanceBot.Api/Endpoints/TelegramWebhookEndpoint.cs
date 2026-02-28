using FinanceBot.Api.Endpoints.Contracts;
using FinanceBot.Application.Abstractions;
using FinanceBot.Infrastructure.Telegram;
using Microsoft.Extensions.Options;

namespace FinanceBot.Api.Endpoints;

public static class TelegramWebhookEndpoint
{
    public static IEndpointRouteBuilder MapTelegramWebhookEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/telegram/webhook", HandleWebhookAsync)
            .WithName("TelegramWebhook")
            .Produces(StatusCodes.Status200OK);

        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(
        TelegramWebhookRequest? update,
        ITelegramCommandRouter telegramCommandRouter,
        ITelegramMessageSender telegramMessageSender,
        IOptions<TelegramOptions> telegramOptions,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        ILogger logger = loggerFactory.CreateLogger("TelegramWebhook");

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

        long targetChatId = telegramOptions.Value.ChatId;
        if (targetChatId == 0)
        {
            logger.LogWarning(
                "Telegram chat id is missing. Configure Telegram:ChatId.");
            return Results.Ok();
        }

        string receivedText = update.Message.Text.Trim();
        DateTimeOffset messageDateUtc = ResolveMessageDateUtc(update.Message);
        string responseText = await telegramCommandRouter.RouteAsync(
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
                exception,
                "Failed to send Telegram reply for chat {ChatId}.",
                targetChatId);
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
