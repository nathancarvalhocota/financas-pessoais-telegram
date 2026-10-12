using System.Net;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceBot.Api.Endpoints;
using FinanceBot.Api.Configuration;
using FinanceBot.Api.Messaging;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceBot.IntegrationTests.Api;

public sealed class TelegramWebhookEndpointTests
{
    [Fact]
    public async Task PostWebhook_CategoryLimitLifecycle_PreservesMessagesPersistenceAndIdentity()
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();

        async Task SendAsync(string text, string expected)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new TelegramWebhookRequest
            {
                Message = new TelegramMessageRequest
                {
                    Chat = new TelegramChatRequest { Id = 998877 },
                    Text = text,
                    Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expected, factory.RecordingTelegramMessageHandler.SentMessages.Last().Text);
        }

        await SendAsync("/limite Mercado", "Nenhum limite definido para Mercado.");
        await SendAsync("/limite Mercado 800,25", "Limite de Mercado definido: R$ 800,25");
        int id;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            var limit = Assert.Single(scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>().LimiteCategorias);
            id = limit.Id;
            Assert.Equal(800.25, limit.Valor);
        }

        await SendAsync("/limite Mercado 100", "Limite de Mercado definido: R$ 100,00");
        await SendAsync("/compra 100, Teste limite, Mercado", "Compra registrada: R$ 100,00 - Mercado\nMercado: R$ 100,00 de R$ 100,00 (100%)");
        await SendAsync("/limite Mercado", "Mercado: R$ 100,00 de R$ 100,00 (100%)");
        await SendAsync("/limite Mercado " + new string('9', 400), "Valor invalido. Use um numero positivo ou 0 para remover o limite.");
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            var limit = Assert.Single(scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>().LimiteCategorias);
            Assert.Equal(id, limit.Id);
            Assert.Equal(100, limit.Valor);
        }

        await SendAsync("/limite Mercado 0", "Limite de Mercado removido");
        await SendAsync("/limite Mercado 0", "Limite de Mercado removido");
        await SendAsync("/limite Mercado", "Nenhum limite definido para Mercado.");
        using IServiceScope verify = factory.Services.CreateScope();
        Assert.Empty(verify.ServiceProvider.GetRequiredService<FinanceBotDbContext>().LimiteCategorias);
    }

    [Fact]
    public async Task PostWebhook_InvalidLimitSyntax_PrecedesCategoryErrorAndDoesNotPersist()
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest { Id = 998877 },
                Text = "/limite Inexistente -1"
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Uso correto: /limite <CATEGORIA> <VALOR>. Exemplo: /limite Mercado 800\nPara consultar: /limite <CATEGORIA>. Exemplo: /limite Mercado",
            Assert.Single(factory.RecordingTelegramMessageHandler.SentMessages).Text);
        using IServiceScope scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>().LimiteCategorias);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("incorrect")]
    [InlineData("multiple")]
    [InlineData("comma-separated")]
    public async Task PostWebhook_WithInvalidSecret_RejectsWithoutEffects(string scenario)
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("X-Telegram-Bot-Api-Secret-Token");
        if (scenario == "incorrect") client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", "incorrect");
        if (scenario == "multiple") client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", new[] { FinanceBotApiFactory.Secret, "incorrect" });
        if (scenario == "comma-separated") client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", $"{FinanceBotApiFactory.Secret},incorrect");

        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", PurchaseRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(factory.RecordingTelegramMessageHandler.SentMessages);
        using IServiceScope scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>().Compras);
    }

    [Fact]
    public async Task PostWebhook_WithAuthenticatedIgnoredUpdate_ReturnsOkWithoutEffects()
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.RecordingTelegramMessageHandler.SentMessages);
        using IServiceScope scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>().Compras);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostWebhook_WithSendFailureAfterPurchase_AcknowledgesAndLogsSafely(bool transportFailure)
    {
        using FinanceBotApiFactory factory = new(998877);
        factory.RecordingTelegramMessageHandler.FailTransport = transportFailure;
        factory.RecordingTelegramMessageHandler.ResponseStatus = HttpStatusCode.Forbidden;
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", PurchaseRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using IServiceScope scope = factory.Services.CreateScope();
        Assert.Single(scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>().Compras);
        string logs = string.Join("\n", factory.Logs.Entries);
        foreach (string sensitive in new[] { "test-token-identifiable", FinanceBotApiFactory.Secret, "PRIVATE_MESSAGE_IDENTIFIABLE", "RAW_BODY_IDENTIFIABLE", "INNER_EXCEPTION_IDENTIFIABLE", "api.telegram.org" })
            Assert.DoesNotContain(sensitive, logs);
        Assert.Contains(transportFailure ? "HttpRequestException" : "403", logs);
        if (transportFailure) Assert.Contains("Failed to send Telegram reply", logs);
    }

    [Fact]
    public async Task PostWebhook_WithCommandFailure_DoesNotAcknowledgeAsSuccess()
    {
        using FinanceBotApiFactory factory = new(998877) { FailCommand = true };
        using HttpClient client = factory.CreateClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostAsJsonAsync("/telegram/webhook", PurchaseRequest()));
        Assert.Empty(factory.RecordingTelegramMessageHandler.SentMessages);
    }

    [Theory]
    [InlineData("Telegram:BotToken", "")]
    [InlineData("Telegram:WebhookSecret", "invalid secret")]
    [InlineData("Telegram:WebhookSecret", "")]
    [InlineData("Telegram:AllowedChatIds", "")]
    public void Startup_WithInvalidOptions_FailsBeforeResolvingDatabase(string key, string value)
    {
        using FinanceBotApiFactory factory = new(998877) { InvalidOption = new(key, value), RejectDatabaseResolution = true };
        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains(key, exception.Message);
        Assert.DoesNotContain("test-token-identifiable", exception.ToString());
        Assert.DoesNotContain(FinanceBotApiFactory.Secret, exception.ToString());
        Assert.False(factory.DatabaseResolved);
        Assert.Empty(factory.RecordingTelegramMessageHandler.SentMessages);
    }

    private static TelegramWebhookRequest PurchaseRequest() => new()
    {
        Message = new TelegramMessageRequest
        {
            Chat = new TelegramChatRequest { Id = 998877 },
            Text = "/compra 58,90, PRIVATE_MESSAGE_IDENTIFIABLE, Mercado",
            Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        }
    };

    [Fact]
    public async Task PostWebhook_WithStartCommand_ReturnsOkAndSendsReply()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(998877);
        HttpClient httpClient = financeBotApiFactory.CreateClient();

        TelegramWebhookRequest webhookRequest = new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest
                {
                    Id = 998877
                },
                Text = "/start"
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        SentTelegramMessage sentTelegramMessage = Assert.Single(
            financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages);
        Assert.Equal(998877, sentTelegramMessage.ChatId);
        Assert.Equal(
            "FinanceBot online. Use /info para ver todos os comandos.",
            sentTelegramMessage.Text);
    }

    [Fact]
    public async Task PostWebhook_WithEmptyText_ReturnsOkWithoutSendingReply()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(998877);
        HttpClient httpClient = financeBotApiFactory.CreateClient();

        TelegramWebhookRequest webhookRequest = new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest
                {
                    Id = 998877
                },
                Text = string.Empty
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        Assert.Empty(financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages);
    }

    [Fact]
    public async Task PostWebhook_WithPurchaseCommand_PersistsExpenseAndSendsConfirmation()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(998877);
        HttpClient httpClient = financeBotApiFactory.CreateClient();

        TelegramWebhookRequest webhookRequest = new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest
                {
                    Id = 998877
                },
                Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Text = "/compra 58,90, Almoco da semana, Mercado"
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        SentTelegramMessage sentTelegramMessage = Assert.Single(
            financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages);
        Assert.Contains("Compra registrada: R$ 58,90", sentTelegramMessage.Text);
        Assert.Contains("Mercado", sentTelegramMessage.Text);

        using IServiceScope serviceScope = financeBotApiFactory.Services.CreateScope();
        FinanceBotDbContext financeBotDbContext = serviceScope.ServiceProvider.GetRequiredService<
            FinanceBotDbContext>();
        Compra persistedCompra = Assert.Single(financeBotDbContext.Compras.ToList());
        Assert.Equal(58.90, persistedCompra.Valor, 2);
        Assert.Equal("Almoco da semana", persistedCompra.Descricao);
        Assert.Equal(Categoria.Mercado, persistedCompra.Categoria);
    }

    [Fact]
    public async Task PostWebhook_WithPurchaseCommandAndUnknownCategory_ReturnsCategoryOptions()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(998877);
        HttpClient httpClient = financeBotApiFactory.CreateClient();

        TelegramWebhookRequest webhookRequest = new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest
                {
                    Id = 998877
                },
                Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Text = "/compra 200, teste, teste"
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        SentTelegramMessage sentTelegramMessage = Assert.Single(
            financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages);
        Assert.Contains("Categoria 'teste' inexistente.", sentTelegramMessage.Text);
        Assert.Contains("Categorias disponiveis:", sentTelegramMessage.Text);
        Assert.Contains("Mercado", sentTelegramMessage.Text);
        Assert.DoesNotContain("Uso correto", sentTelegramMessage.Text);

        using IServiceScope serviceScope = financeBotApiFactory.Services.CreateScope();
        FinanceBotDbContext financeBotDbContext = serviceScope.ServiceProvider.GetRequiredService<
            FinanceBotDbContext>();
        Assert.Empty(financeBotDbContext.Compras.ToList());
    }

    [Fact]
    public async Task PostWebhook_WithListCommand_ReturnsMonthExpenses()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(998877);
        HttpClient httpClient = financeBotApiFactory.CreateClient();
        DateTime januaryDate = new DateTime(2026, 1, 20, 10, 30, 0, DateTimeKind.Utc);

        using (IServiceScope serviceScope = financeBotApiFactory.Services.CreateScope())
        {
            FinanceBotDbContext financeBotDbContext = serviceScope.ServiceProvider.GetRequiredService<
                FinanceBotDbContext>();
            Compra compra1 = new Compra
            {
                Valor = 58.90,
                Descricao = "Almoco",
                Categoria = Categoria.Mercado,
                Data = januaryDate
            };
            Compra compra2 = new Compra
            {
                Valor = 20.00,
                Descricao = "Lanche",
                Categoria = Categoria.Lanches,
                Data = januaryDate.AddDays(1)
            };
            financeBotDbContext.Compras.AddRange(compra1, compra2);
            financeBotDbContext.SaveChanges();
        }

        TelegramWebhookRequest webhookRequest = new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest
                {
                    Id = 998877
                },
                Text = "/listar 01/26"
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        SentTelegramMessage sentTelegramMessage = Assert.Single(
            financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages);
        Assert.Contains("Compras de 15/01 a 14/02", sentTelegramMessage.Text);
        Assert.Contains("Almoco", sentTelegramMessage.Text);
        Assert.Contains("Lanche", sentTelegramMessage.Text);
        Assert.Contains("Total: R$ 78,90", sentTelegramMessage.Text);
    }

    [Fact]
    public async Task PostWebhook_WithDeleteCommand_RemovesExpenseAndReturnsSummary()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(998877);
        HttpClient httpClient = financeBotApiFactory.CreateClient();
        int compraId;

        using (IServiceScope serviceScope = financeBotApiFactory.Services.CreateScope())
        {
            FinanceBotDbContext financeBotDbContext = serviceScope.ServiceProvider.GetRequiredService<
                FinanceBotDbContext>();
            Compra compra = new Compra
            {
                Valor = 33.50,
                Descricao = "Uber para trabalho",
                Categoria = Categoria.Uber,
                Data = DateTime.UtcNow
            };
            financeBotDbContext.Compras.Add(compra);
            financeBotDbContext.SaveChanges();
            compraId = compra.Id;
        }

        TelegramWebhookRequest webhookRequest = new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest
                {
                    Id = 998877
                },
                Text = $"/deletar {compraId}"
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        SentTelegramMessage sentTelegramMessage = Assert.Single(
            financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages);
        Assert.Contains("Compra removida: R$ 33,50", sentTelegramMessage.Text);
        Assert.Contains("Uber para trabalho", sentTelegramMessage.Text);

        using IServiceScope verifyScope = financeBotApiFactory.Services.CreateScope();
        FinanceBotDbContext verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<
            FinanceBotDbContext>();
        Assert.Empty(verifyDbContext.Compras.ToList());
    }

    [Fact]
    public async Task PostWebhook_WithMissingChatPayload_ReturnsOkWithoutSendingReply()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(998877);
        HttpClient httpClient = financeBotApiFactory.CreateClient();

        TelegramWebhookRequest webhookRequest = new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Text = "/start"
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        Assert.Empty(financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages);
    }

    [Fact]
    public async Task PostWebhook_WithUnauthorizedChat_ReturnsOkWithoutSendingReplyOrPersisting()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(112233);
        HttpClient httpClient = financeBotApiFactory.CreateClient();

        TelegramWebhookRequest webhookRequest = new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest
                {
                    Id = 998877
                },
                Date = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Text = "/compra 58,90, Almoco da semana, Mercado"
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        Assert.Empty(financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages);

        using IServiceScope serviceScope = financeBotApiFactory.Services.CreateScope();
        FinanceBotDbContext financeBotDbContext = serviceScope.ServiceProvider.GetRequiredService<
            FinanceBotDbContext>();
        Assert.Empty(financeBotDbContext.Compras.ToList());
    }

    private sealed class FinanceBotApiFactory : WebApplicationFactory<Program>
    {
        public const string Secret = "test-webhook-secret-identifiable";
        public KeyValuePair<string, string?>? InvalidOption { get; init; }
        public bool RejectDatabaseResolution { get; init; }
        public bool DatabaseResolved { get; private set; }
        public bool FailCommand { get; init; }
        public CapturingLoggerProvider Logs { get; } = new();
        public FinanceBotApiFactory(params long[] allowedChatIds)
        {
            AllowedChatIds = allowedChatIds;
            DatabaseName = $"FinanceBotTests_{Guid.NewGuid()}";
        }

        private IReadOnlyList<long> AllowedChatIds { get; }

        private string DatabaseName { get; }

        public RecordingTelegramMessageHandler RecordingTelegramMessageHandler { get; } =
            new RecordingTelegramMessageHandler();

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Infrastructure reads its connection string during service registration,
            // before ConfigureWebHost's application configuration callbacks run.
            builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Host=localhost;Port=5432;Database=fake;Username=fake;Password=fake"
                }));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(Logs));

            Dictionary<string, string?> testConfiguration = new Dictionary<string, string?>
            {
                ["Telegram:BotToken"] = "test-token-identifiable",
                ["Telegram:WebhookSecret"] = Secret,
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=fake;Username=fake;Password=fake"
            };
            for (int index = 0; index < AllowedChatIds.Count; index++)
            {
                testConfiguration[$"Telegram:AllowedChatIds:{index}"] =
                    AllowedChatIds[index].ToString();
            }
            if (InvalidOption is { } invalid)
            {
                if (invalid.Key == "Telegram:AllowedChatIds")
                    testConfiguration.Remove("Telegram:AllowedChatIds:0");
                else
                    testConfiguration[invalid.Key] = invalid.Value;
            }

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(testConfiguration);
            });

            builder.ConfigureServices(services =>
            {
                // Keep fixture options independent of developer secrets/environment.
                services.RemoveAll<IConfigureOptions<TelegramOptions>>();
                services.Configure<TelegramOptions>(new ConfigurationBuilder()
                    .AddInMemoryCollection(testConfiguration).Build().GetSection("Telegram"));
                services.RemoveAll<DbContextOptions<FinanceBotDbContext>>();
                services.RemoveAll<FinanceBotDbContext>();
                services.AddDbContext<FinanceBotDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DatabaseName);
                });
                if (RejectDatabaseResolution)
                {
                    services.AddScoped<FinanceBotDbContext>(_ =>
                    {
                        DatabaseResolved = true;
                        throw new InvalidOperationException("Database must not be resolved before options validation.");
                    });
                }
                if (FailCommand) services.AddScoped<ICompraRepository, FailingCompraRepository>();

                services.RemoveAll<TelegramMessageSender>();
                services.AddHttpClient<TelegramMessageSender>()
                    .ConfigurePrimaryHttpMessageHandler(() => RecordingTelegramMessageHandler);
            });
        }

        protected override void ConfigureClient(HttpClient client)
        {
            base.ConfigureClient(client);
            client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", Secret);
        }
    }

    private sealed class RecordingTelegramMessageHandler : HttpMessageHandler
    {
        public bool FailTransport { get; set; }
        public HttpStatusCode ResponseStatus { get; set; } = HttpStatusCode.OK;
        public List<SentTelegramMessage> SentMessages { get; } = new List<SentTelegramMessage>();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string requestContent = await request.Content!.ReadAsStringAsync(cancellationToken);
            using JsonDocument document = JsonDocument.Parse(requestContent);
            JsonElement payload = document.RootElement;
            SentTelegramMessage sentTelegramMessage = new SentTelegramMessage(
                payload.GetProperty("chat_id").GetInt64(),
                payload.GetProperty("text").GetString() ?? string.Empty);
            SentMessages.Add(sentTelegramMessage);
            if (FailTransport)
                throw new HttpRequestException($"{request.RequestUri} {FinanceBotApiFactory.Secret} PRIVATE_MESSAGE_IDENTIFIABLE",
                    new Exception("INNER_EXCEPTION_IDENTIFIABLE test-token-identifiable"));
            return new HttpResponseMessage(ResponseStatus)
            {
                Content = new StringContent($"RAW_BODY_IDENTIFIABLE {FinanceBotApiFactory.Secret} test-token-identifiable")
            };
        }
    }

    private sealed class FailingCompraRepository : ICompraRepository
    {
        public Task AddAsync(Compra compra, CancellationToken cancellationToken) => throw new InvalidOperationException("Command persistence failed.");
        public Task<IReadOnlyList<Compra>> ListByPeriodAsync(DateTime periodStartUtc, DateTime periodEndExclusiveUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Compra>>([]);
        public Task<Compra?> FindByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Compra compra, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => entries.Enqueue($"{category}: {formatter(state, exception)} {exception}");
    }

    private sealed record SentTelegramMessage(long ChatId, string Text);
}
