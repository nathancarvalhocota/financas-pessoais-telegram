using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FinanceBot.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceBot.Infrastructure.Telegram;

public sealed class TelegramMessageSender : ITelegramMessageSender
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
        if (string.IsNullOrWhiteSpace(botToken))
        {
            _logger.LogWarning(
                "Telegram bot token is missing. Configure Telegram:BotToken to enable sendMessage.");
            return;
        }

        string requestUri = $"https://api.telegram.org/bot{botToken}/sendMessage";
        SendMessageRequest request = new SendMessageRequest(chatId, text);
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
            requestUri,
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Telegram sendMessage failed with status code {StatusCode}. Body: {Body}",
            (int)response.StatusCode,
            responseContent);
    }

    private sealed record SendMessageRequest(
        [property: JsonPropertyName("chat_id")] long ChatId,
        [property: JsonPropertyName("text")] string Text);
}
