using System.Net;
using System.Net.Http.Json;
using FinanceBot.Api.Endpoints.Contracts;
using FinanceBot.Application.Abstractions;
using FinanceBot.Domain.Entities;
using FinanceBot.Domain.Enums;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FinanceBot.IntegrationTests.Api;

public sealed class TelegramWebhookEndpointTests
{
    [Fact]
    public async Task PostWebhook_WithStartCommand_ReturnsOkAndSendsReply()
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
                Text = "/start"
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        SentTelegramMessage sentTelegramMessage = Assert.Single(
            financeBotApiFactory.RecordingTelegramMessageSender.SentMessages);
        Assert.Equal(112233, sentTelegramMessage.ChatId);
        Assert.Equal(
            "FinanceBot online. Use /compra, /listar ou /deletar.",
            sentTelegramMessage.Text);
    }

    [Fact]
    public async Task PostWebhook_WithEmptyText_ReturnsOkWithoutSendingReply()
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
                Text = string.Empty
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        Assert.Empty(financeBotApiFactory.RecordingTelegramMessageSender.SentMessages);
    }

    [Fact]
    public async Task PostWebhook_WithCompraCommand_PersistsExpenseAndSendsConfirmation()
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
        SentTelegramMessage sentTelegramMessage = Assert.Single(
            financeBotApiFactory.RecordingTelegramMessageSender.SentMessages);
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
    public async Task PostWebhook_WithCompraCommandAndUnknownCategory_ReturnsCategoryOptions()
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
                Text = "/compra 200, teste, teste"
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        SentTelegramMessage sentTelegramMessage = Assert.Single(
            financeBotApiFactory.RecordingTelegramMessageSender.SentMessages);
        Assert.Contains("Categoria inexistente: teste.", sentTelegramMessage.Text);
        Assert.Contains("Categorias disponiveis:", sentTelegramMessage.Text);
        Assert.Contains("Mercado", sentTelegramMessage.Text);
        Assert.DoesNotContain("Uso correto", sentTelegramMessage.Text);

        using IServiceScope serviceScope = financeBotApiFactory.Services.CreateScope();
        FinanceBotDbContext financeBotDbContext = serviceScope.ServiceProvider.GetRequiredService<
            FinanceBotDbContext>();
        Assert.Empty(financeBotDbContext.Compras.ToList());
    }

    [Fact]
    public async Task PostWebhook_WithListarCommand_ReturnsMonthExpenses()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(112233);
        HttpClient httpClient = financeBotApiFactory.CreateClient();
        DateTime januaryDate = new DateTime(2026, 1, 10, 10, 30, 0, DateTimeKind.Utc);

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
                Categoria = Categoria.RestauranteLanche,
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
            financeBotApiFactory.RecordingTelegramMessageSender.SentMessages);
        Assert.Contains("Compras de 01/26", sentTelegramMessage.Text);
        Assert.Contains("Almoco", sentTelegramMessage.Text);
        Assert.Contains("Lanche", sentTelegramMessage.Text);
        Assert.Contains("Total: R$ 78,90", sentTelegramMessage.Text);
    }

    [Fact]
    public async Task PostWebhook_WithDeletarCommand_RemovesExpenseAndReturnsSummary()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(112233);
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
            financeBotApiFactory.RecordingTelegramMessageSender.SentMessages);
        Assert.Contains("Compra removida: R$ 33,50", sentTelegramMessage.Text);
        Assert.Contains("Uber para trabalho", sentTelegramMessage.Text);

        using IServiceScope verifyScope = financeBotApiFactory.Services.CreateScope();
        FinanceBotDbContext verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<
            FinanceBotDbContext>();
        Assert.Empty(verifyDbContext.Compras.ToList());
    }

    [Fact]
    public async Task PostWebhook_WithMissingChatIdConfig_ReturnsOkWithoutSendingReply()
    {
        using FinanceBotApiFactory financeBotApiFactory = new FinanceBotApiFactory(0);
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
        Assert.Empty(financeBotApiFactory.RecordingTelegramMessageSender.SentMessages);
    }

    private sealed class FinanceBotApiFactory : WebApplicationFactory<Program>
    {
        public FinanceBotApiFactory(long chatId)
        {
            ChatId = chatId;
            DatabaseName = $"FinanceBotTests_{Guid.NewGuid()}";
        }

        private long ChatId { get; }

        private string DatabaseName { get; }

        public RecordingTelegramMessageSender RecordingTelegramMessageSender { get; } =
            new RecordingTelegramMessageSender();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Dictionary<string, string?> testConfiguration = new Dictionary<string, string?>
            {
                ["Telegram:BotToken"] = "test-token",
                ["Telegram:ChatId"] = ChatId.ToString(),
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=localhost;Port=5432;Database=fake;Username=fake;Password=fake"
            };

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(testConfiguration);
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<FinanceBotDbContext>>();
                services.RemoveAll<FinanceBotDbContext>();
                services.AddDbContext<FinanceBotDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DatabaseName);
                });

                services.RemoveAll<ITelegramMessageSender>();
                services.AddSingleton<ITelegramMessageSender>(RecordingTelegramMessageSender);
            });
        }
    }

    private sealed class RecordingTelegramMessageSender : ITelegramMessageSender
    {
        public List<SentTelegramMessage> SentMessages { get; } = new List<SentTelegramMessage>();

        public Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            SentTelegramMessage sentTelegramMessage = new SentTelegramMessage(chatId, text);
            SentMessages.Add(sentTelegramMessage);
            return Task.CompletedTask;
        }
    }

    private sealed record SentTelegramMessage(long ChatId, string Text);
}
