using System.Diagnostics;
using FinanceBot.Api.Commands;
using FinanceBot.Api.Commands.Parsing;
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
        long started = Stopwatch.GetTimestamp();
        string outcome = "failed";
        string stage = "authentication";
        string? recognizedCommand = null;
        string? exceptionType = null;

        try
        {
            if (!request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secrets)
                || secrets.Count != 1
                || !string.Equals(secrets[0], telegramOptions.Value.WebhookSecret, StringComparison.Ordinal))
            {
                outcome = "rejected";
                return Results.Unauthorized();
            }

            stage = "validateUpdate";
            if (update?.Message is null)
            {
                outcome = "ignored";
                return Results.Ok();
            }

            if (string.IsNullOrWhiteSpace(update.Message.Text))
            {
                outcome = "ignored";
                return Results.Ok();
            }

            if (update.Message.Chat is null || update.Message.Chat.Id == 0)
            {
                outcome = "ignored";
                return Results.Ok();
            }

            long targetChatId = update.Message.Chat.Id;
            if (!telegramOptions.Value.AllowedChatIds.Contains(targetChatId))
            {
                outcome = "ignored";
                return Results.Ok();
            }

            string receivedText = update.Message.Text.Trim();
            // Unknown command tokens can contain private payloads; only log the supported names.
            recognizedCommand = TelegramCommandParser.Parse(receivedText).Name switch
            {
                "/compra" => "/compra",
                "/listar" => "/listar",
                "/deletar" => "/deletar",
                "/limite" => "/limite",
                "/start" => "/start",
                "/info" => "/info",
                _ => null
            };
            stage = "command";
            DateTimeOffset messageDateUtc = ResolveMessageDateUtc(update.Message);

            string responseText = await telegramCommandRouter.RouteMessageAsync(
                receivedText,
                messageDateUtc,
                cancellationToken);

            outcome = "processed";
            stage = "sendMessage";
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
        catch (Exception exception)
        {
            outcome = "failed";
            exceptionType = exception.GetType().Name;
            throw;
        }
        finally
        {
            logger.Log(
                outcome == "failed" ? LogLevel.Error : outcome == "rejected" ? LogLevel.Warning : LogLevel.Information,
                new EventId(1001, "TelegramWebhookCompleted"),
                "Telegram webhook processing completed. Request: {RequestId}; duration ms: {DurationMs}; outcome: {Outcome}; command: {Command}; stage: {Stage}; exception type: {ExceptionType}.",
                request.HttpContext.TraceIdentifier,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                outcome,
                recognizedCommand,
                stage,
                exceptionType);
        }
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
