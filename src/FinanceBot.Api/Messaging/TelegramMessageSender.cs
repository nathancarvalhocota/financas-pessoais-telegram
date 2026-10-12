using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FinanceBot.Api.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceBot.Api.Messaging;

public sealed class TelegramMessageSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramMessageSender> _logger;
    private readonly IOptions<TelegramOptions> _telegramOptions;

    public TelegramMessageSender(
        HttpClient httpClient,
        ILogger<TelegramMessageSender> logger,
        IOptions<TelegramOptions> telegramOptions)
    {
        _httpClient = httpClient;
        _logger = logger;
        _telegramOptions = telegramOptions;
    }

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        string botToken = _telegramOptions.Value.BotToken;
        string requestUri = $"https://api.telegram.org/bot{botToken}/sendMessage";
        SendMessageRequest request = new SendMessageRequest(chatId, text);
        try
        {
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                requestUri,
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Telegram sendMessage failed. Stage: {Stage}; status code: {StatusCode}.",
                    "sendMessage", (int)response.StatusCode);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "Telegram sendMessage failed. Stage: {Stage}; exception type: {ExceptionType}; status: {StatusCode}.",
                "sendMessage", exception.GetType().Name,
                exception is HttpRequestException httpException ? (int?)httpException.StatusCode : null);
            throw;
        }
    }

    private sealed record SendMessageRequest(
        [property: JsonPropertyName("chat_id")] long ChatId,
        [property: JsonPropertyName("text")] string Text);
}
