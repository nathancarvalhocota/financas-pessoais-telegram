using System.Text.Json.Serialization;

namespace FinanceBot.Api.Endpoints.Contracts;

public sealed class TelegramWebhookRequest
{
    [JsonPropertyName("message")]
    public TelegramMessageRequest? Message { get; init; }
}

public sealed class TelegramMessageRequest
{
    [JsonPropertyName("chat")]
    public TelegramChatRequest? Chat { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("date")]
    public long? Date { get; init; }
}

public sealed class TelegramChatRequest
{
    [JsonPropertyName("id")]
    public long Id { get; init; }
}
