using System.Net;
using System.Text;
using System.Text.Json;
using FinanceBot.Infrastructure.Telegram;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceBot.UnitTests.Infrastructure;

public sealed class TelegramMessageSenderTests
{
    [Fact]
    public async Task SendMessageAsync_WithValidToken_SendsExpectedRequest()
    {
        RecordingHttpMessageHandler recordingHttpMessageHandler = new RecordingHttpMessageHandler();
        HttpClient httpClient = new HttpClient(recordingHttpMessageHandler);
        IOptions<TelegramOptions> telegramOptions = Options.Create(
            new TelegramOptions
            {
                BotToken = "123456:ABCDEF"
            });

        TelegramMessageSender telegramMessageSender = new TelegramMessageSender(
            httpClient,
            NullLogger<TelegramMessageSender>.Instance,
            telegramOptions);

        await telegramMessageSender.SendMessageAsync(9999, "Mensagem de teste", CancellationToken.None);

        HttpRequestMessage? sentRequestCandidate = recordingHttpMessageHandler.LastRequest;
        Assert.NotNull(sentRequestCandidate);
        HttpRequestMessage sentRequest = sentRequestCandidate;
        Assert.Equal(HttpMethod.Post, sentRequest.Method);
        Assert.Equal(
            "https://api.telegram.org/bot123456:ABCDEF/sendMessage",
            sentRequest.RequestUri?.ToString());

        HttpContent? sentContentCandidate = sentRequest.Content;
        Assert.NotNull(sentContentCandidate);
        HttpContent sentContent = sentContentCandidate;
        string requestBody = await sentContent.ReadAsStringAsync();
        JsonDocument jsonDocument = JsonDocument.Parse(requestBody);
        JsonElement jsonRoot = jsonDocument.RootElement;

        Assert.Equal(9999, jsonRoot.GetProperty("chat_id").GetInt64());
        Assert.Equal("Mensagem de teste", jsonRoot.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendMessageAsync_WithoutToken_DoesNotCallTelegramApi()
    {
        RecordingHttpMessageHandler recordingHttpMessageHandler = new RecordingHttpMessageHandler();
        HttpClient httpClient = new HttpClient(recordingHttpMessageHandler);
        IOptions<TelegramOptions> telegramOptions = Options.Create(
            new TelegramOptions
            {
                BotToken = string.Empty
            });

        TelegramMessageSender telegramMessageSender = new TelegramMessageSender(
            httpClient,
            NullLogger<TelegramMessageSender>.Instance,
            telegramOptions);

        await telegramMessageSender.SendMessageAsync(9999, "Mensagem de teste", CancellationToken.None);

        Assert.Null(recordingHttpMessageHandler.LastRequest);
    }

    [Fact]
    public async Task SendMessageAsync_WithTelegramError_DoesNotThrowException()
    {
        RecordingHttpMessageHandler recordingHttpMessageHandler = new RecordingHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.NotFound,
            ResponseBody = "{\"ok\":false,\"error_code\":404,\"description\":\"Not Found\"}"
        };

        HttpClient httpClient = new HttpClient(recordingHttpMessageHandler);
        IOptions<TelegramOptions> telegramOptions = Options.Create(
            new TelegramOptions
            {
                BotToken = "123456:ABCDEF"
            });

        TelegramMessageSender telegramMessageSender = new TelegramMessageSender(
            httpClient,
            NullLogger<TelegramMessageSender>.Instance,
            telegramOptions);

        Exception? exception = await Record.ExceptionAsync(() =>
            telegramMessageSender.SendMessageAsync(9999, "Mensagem de teste", CancellationToken.None));

        Assert.Null(exception);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public HttpStatusCode ResponseStatusCode { get; init; } = HttpStatusCode.OK;

        public string ResponseBody { get; init; } = "{\"ok\":true}";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            HttpContent responseContent = new StringContent(
                ResponseBody,
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage httpResponseMessage = new HttpResponseMessage(ResponseStatusCode)
            {
                Content = responseContent
            };

            return Task.FromResult(httpResponseMessage);
        }
    }
}
