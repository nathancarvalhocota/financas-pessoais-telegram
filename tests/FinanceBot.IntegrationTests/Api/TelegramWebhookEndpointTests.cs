using System.Net;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceBot.Api.Endpoints;
using FinanceBot.Api.Configuration;
using FinanceBot.Api.Messaging;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Expenses;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.IntegrationTests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
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
    [Theory]
    [InlineData("/listar", "15/04 a 14/05", "", true)]
    [InlineData("/listar 04/26", "15/04 a 14/05", "", true)]
    [InlineData("/listar 04/26 Uber, Mercado, Uber", "15/04 a 14/05", " (Uber, Mercado, Uber)", false)]
    [InlineData("/listar 04/26 sem Lazer, Lazer", "15/04 a 14/05", " (sem Lazer, Lazer)", false)]
    [InlineData("/listar 15/04/26 - 14/05/2026", "15/04/2026 a 14/05/2026", "", true)]
    [InlineData("/listar 15/04/26 - 14/05/2026 Uber, Mercado, Uber", "15/04/2026 a 14/05/2026", " (Uber, Mercado, Uber)", false)]
    [InlineData("/listar 15/04/26 - 14/05/2026 sem Lazer, Lazer", "15/04/2026 a 14/05/2026", " (sem Lazer, Lazer)", false)]
    public async Task PostWebhook_List_PreservesBoundariesFiltersOrderTotalsAndVisibleUtc(
        string command, string period, string suffix, bool showLeisure)
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        int firstId, middleId, lastId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>();
            Compra Create(double amount, string description, Categoria category, DateTime date) =>
                Assert.IsType<CompraCreationResult.Success>(Compra.Criar(amount, description, category, date)).Compra;
            DateTime start = new(2026, 4, 15, 3, 0, 0, DateTimeKind.Utc);
            DateTime end = new(2026, 5, 15, 3, 0, 0, DateTimeKind.Utc);
            Compra first = Create(10.25, "Primeiro instante", Categoria.Mercado, start);
            Compra middle = Create(20, "Cinema", Categoria.Lazer, start.AddDays(5));
            Compra last = Create(30.5, "Último instante", Categoria.Uber, end.AddTicks(-1));
            context.Compras.AddRange(first, last, middle,
                Create(100, "Antes", Categoria.Mercado, start.AddTicks(-1)),
                Create(200, "Depois", Categoria.Uber, end));
            await context.SaveChangesAsync();
            firstId = first.Id;
            middleId = middle.Id;
            lastId = last.Id;
        }

        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest { Id = 998877 }, Text = command,
                Date = new DateTimeOffset(2026, 4, 15, 3, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string newline = Environment.NewLine;
        string expected = $"Compras de {period}{suffix}:{newline}" +
            $"{newline}[{lastId}] Uber — R$ 30,50{newline}    Último instante · 15/05 02:59{newline}";
        if (showLeisure) expected += $"{newline}[{middleId}] Lazer — R$ 20,00{newline}    Cinema · 20/04 03:00{newline}";
        expected += $"{newline}[{firstId}] Mercado — R$ 10,25{newline}    Primeiro instante · 15/04 03:00{newline}" +
            $"\nTotal: R$ {(showLeisure ? "60,75" : "40,75")}";
        Assert.Equal(expected, Assert.Single(factory.RecordingTelegramMessageHandler.SentMessages).Text);
        using IServiceScope verify = factory.Services.CreateScope();
        Assert.Equal(5, verify.ServiceProvider.GetRequiredService<FinanceBotDbContext>().Compras.Count());
    }

    [Theory]
    [InlineData("/listar", "Nenhuma compra encontrada de 15/03 a 14/04.")]
    [InlineData("/listar Lazer, Lazer", "Nenhuma compra de Lazer, Lazer de 15/03 a 14/04.")]
    [InlineData("/listar sem Mercado, Mercado", "Nenhuma compra de 15/03 a 14/04 (sem Mercado, Mercado).")]
    [InlineData("/listar 10/02/26 - 20/02/2026", "Nenhuma compra encontrada de 10/02/2026 a 20/02/2026.")]
    [InlineData("/listar@FinanceBot 13/26", "Uso correto: /listar <MM/YY>. Exemplo: /listar 09/26")]
    [InlineData("/listar 31/02/26 - 10/02/2026 sem", "Data invalida. Use DD/MM/AA ou DD/MM/AAAA. Exemplo: /listar 10/02/26 - 20/02/2026")]
    [InlineData("/listar 20/02/26 - 10/02/2026 sem", "Data inicial nao pode ser posterior a data final.")]
    [InlineData("/listar 13/26 sem", "Mes/ano invalido. Use o formato MM/YY, exemplo: 09/26")]
    [InlineData("/listar sem", "Informe ao menos uma categoria após 'sem'. Exemplo: /listar sem Mercado")]
    [InlineData("/listar Mercado, desconhecida, outra", "Categoria 'desconhecida' inexistente.\n\nCategorias disponiveis:\nEducacao\nLazer\nLanches\nUber\nMercado\nMoto\nCompras\nOutros\nEstetica\nLimpeza\nSaude")]
    public async Task PostWebhook_ListEmptyOrInvalid_PreservesMessagesAndMessageDate(string command, string expected)
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest { Id = 998877 }, Text = command,
                Date = new DateTimeOffset(2026, 4, 15, 2, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds()
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, Assert.Single(factory.RecordingTelegramMessageHandler.SentMessages).Text);
        using IServiceScope verify = factory.Services.CreateScope();
        Assert.Empty(verify.ServiceProvider.GetRequiredService<FinanceBotDbContext>().Compras);
    }

    [Theory]
    [InlineData(125, "⚠️ ALERTA AMARELO: gasto do ciclo acima do anterior")]
    [InlineData(150, "🚨 ALERTA VERMELHO: gasto do ciclo muito acima do anterior")]
    [InlineData(200, "🚨 ALERTA URGENTE: gasto do ciclo o dobro do anterior")]
    public async Task PostWebhook_PurchaseWithLimitAndCrossedThreshold_PersistsAndPresentsHighestAlert(int amount, string heading)
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>();
            context.Compras.Add(Assert.IsType<CompraCreationResult.Success>(Compra.Criar(
                100, "Base anterior", Categoria.Mercado, new DateTime(2026, 4, 14, 12, 0, 0, DateTimeKind.Utc))).Compra);
            context.LimiteCategorias.Add(Assert.IsType<LimiteCategoriaCreationResult.Success>(LimiteCategoria.Criar(Categoria.Mercado, 100)).Limite);
            await context.SaveChangesAsync();
        }

        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest { Id = 998877 },
                Text = $"/compra {amount}, Compra atual, Mercado",
                Date = new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string reply = Assert.Single(factory.RecordingTelegramMessageHandler.SentMessages).Text;
        Assert.StartsWith($"Compra registrada: R$ {amount},00 - Mercado\n⚠ Limite de Mercado ultrapassado: R$ {amount},00 de R$ 100,00\n\n{heading}", reply);
        Assert.Contains($"Ciclo atual (15/04/2026 a 14/05/2026): R$ {amount},00", reply);
        Assert.Contains("Ciclo anterior (15/03/2026 a 14/04/2026): R$ 100,00", reply);
        Assert.Contains($"Gasto atual: {amount}% do ciclo anterior (+R$ {amount - 100},00).", reply);
        Assert.Contains("• Educacao: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)", reply);
        using IServiceScope verify = factory.Services.CreateScope();
        var saved = Assert.Single(verify.ServiceProvider.GetRequiredService<FinanceBotDbContext>().Compras, item => item.Descricao == "Compra atual");
        Assert.Equal(amount, saved.Valor);
        Assert.Equal(new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc), saved.Data);
        Assert.True(saved.Id > 0);
    }

    [Theory]
    [InlineData("/compra -1, Teste, Inexistente", "Uso correto: /compra <VALOR>, <DESCRICAO>, <CATEGORIA>. Exemplo: /compra 58,90, Almoco, Mercado")]
    [InlineData("/compra 0,   , Inexistente", "Valor invalido. Use um numero positivo, exemplo: 58,90")]
    [InlineData("/compra 1,   , Inexistente", "Descricao obrigatoria no comando /compra.")]
    public async Task PostWebhook_InvalidPurchase_PreservesErrorPrecedenceAndDoesNotPersist(string text, string expected)
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest { Chat = new TelegramChatRequest { Id = 998877 }, Text = text }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, Assert.Single(factory.RecordingTelegramMessageHandler.SentMessages).Text);
        using IServiceScope verify = factory.Services.CreateScope();
        Assert.Empty(verify.ServiceProvider.GetRequiredService<FinanceBotDbContext>().Compras);
    }

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
        if (scenario == "incorrect") client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", "INVALID_HEADER_PRIVATE");
        if (scenario == "multiple") client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", new[] { FinanceBotApiFactory.Secret, "incorrect" });
        if (scenario == "comma-separated") client.DefaultRequestHeaders.Add("X-Telegram-Bot-Api-Secret-Token", $"{FinanceBotApiFactory.Secret},incorrect");

        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", PurchaseRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertCompletion(factory, "rejected", null);
        Assert.Empty(factory.RecordingTelegramMessageHandler.SentMessages);
        string logs = string.Join("\n", factory.Logs.Entries);
        Assert.DoesNotContain("INVALID_HEADER_PRIVATE", logs);
        Assert.DoesNotContain(FinanceBotApiFactory.Secret, logs);
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
        AssertCompletion(factory, "ignored", null);
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
        foreach (string sensitive in new[] { "test-token-identifiable", FinanceBotApiFactory.Secret, "PRIVATE_MESSAGE_IDENTIFIABLE", "58,90", "RAW_BODY_IDENTIFIABLE", "INNER_EXCEPTION_IDENTIFIABLE", "api.telegram.org" })
            Assert.DoesNotContain(sensitive, logs);
        Assert.Contains(transportFailure ? "HttpRequestException" : "403", logs);
        if (transportFailure) Assert.Contains("Failed to send Telegram reply", logs);
        AssertCompletion(factory, "processed", "/compra");
    }

    [Fact]
    public async Task PostWebhook_WithCommandFailure_DoesNotAcknowledgeAsSuccess()
    {
        using FinanceBotApiFactory factory = new(998877) { FailCommand = true };
        using HttpClient client = factory.CreateClient();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PostAsJsonAsync("/telegram/webhook", PurchaseRequest()));
        Assert.Empty(factory.RecordingTelegramMessageHandler.SentMessages);
        CapturedLog completion = AssertCompletion(factory, "failed", "/compra");
        Assert.Equal("command", completion.Properties["Stage"]);
        Assert.Equal("InvalidOperationException", completion.Properties["ExceptionType"]);
        Assert.Equal(LogLevel.Error, completion.Level);
        string logs = string.Join("\n", factory.Logs.Entries);
        foreach (string marker in new[] { "COMMAND_EXCEPTION_PRIVATE", "test-token-identifiable", FinanceBotApiFactory.Secret, "PRIVATE_MESSAGE_IDENTIFIABLE", "58,90", "api.telegram.org" })
            Assert.DoesNotContain(marker, logs);
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
        Assert.DoesNotContain(factory.Logs.Events, item => item.EventId.Name == "DatabaseInitializationStarted");
    }

    [Theory]
    [InlineData("  /START@PrivateBotName argument_private  ", "/start")]
    [InlineData("/info", "/info")]
    [InlineData("/compra", "/compra")]
    [InlineData("/listar", "/listar")]
    [InlineData("/deletar", "/deletar")]
    [InlineData("/limite", "/limite")]
    [InlineData("/UNKNOWN_PRIVATE_PAYLOAD argument_private", null)]
    [InlineData("PRIVATE_NON_COMMAND_PAYLOAD", null)]
    public async Task PostWebhook_Completion_OnlyIncludesRecognizedCommand(string text, string? command)
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest { Chat = new TelegramChatRequest { Id = 998877 }, Text = text }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        CapturedLog completion = AssertCompletion(factory, "processed", command);
        Assert.Equal(LogLevel.Information, completion.Level);
        string logs = string.Join("\n", factory.Logs.Entries);
        foreach (string sensitive in new[] { "PrivateBotName", "argument_private", "UNKNOWN_PRIVATE_PAYLOAD", "PRIVATE_NON_COMMAND_PAYLOAD", FinanceBotApiFactory.Secret, "test-token-identifiable" })
            Assert.DoesNotContain(sensitive, logs);
    }

    [Theory]
    [InlineData("missing-text")]
    [InlineData("missing-chat")]
    [InlineData("zero-chat")]
    [InlineData("unauthorized-chat")]
    public async Task PostWebhook_IgnoredCompletion_DoesNotIncludePrivateText(string scenario)
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = scenario == "missing-chat" ? null : new TelegramChatRequest { Id = scenario == "zero-chat" ? 0 : scenario == "unauthorized-chat" ? 1 : 998877 },
                Text = scenario == "missing-text" ? "  " : "/compra 987654321, PRIVATE_IGNORED_DESCRIPTION, Mercado"
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCompletion(factory, "ignored", null);
        Assert.Empty(factory.RecordingTelegramMessageHandler.SentMessages);
        string logs = string.Join("\n", factory.Logs.Entries);
        Assert.DoesNotContain("PRIVATE_IGNORED_DESCRIPTION", logs);
        Assert.DoesNotContain("987654321", logs);
    }

    [Fact]
    public async Task PostWebhook_Completions_KeepExistingDistinctHttpIdentifiers()
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        for (int index = 0; index < 2; index++)
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new { });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var identifiers = factory.Logs.Events.Where(item => item.EventId.Name == "TelegramWebhookCompleted")
            .Select(item => Assert.IsType<string>(item.Properties["RequestId"])).ToArray();
        Assert.Equal(factory.RequestIds.ToArray(), identifiers);
        Assert.Equal(2, identifiers.Distinct().Count());
    }

    private static CapturedLog AssertCompletion(FinanceBotApiFactory factory, string outcome, string? command)
    {
        CapturedLog completion = Assert.Single(factory.Logs.Events, item => item.EventId.Name == "TelegramWebhookCompleted");
        Assert.Equal("TelegramWebhook", completion.Category);
        Assert.Equal(outcome, completion.Properties["Outcome"]);
        Assert.Equal(command, completion.Properties["Command"]);
        Assert.Equal(Assert.Single(factory.RequestIds), completion.Properties["RequestId"]);
        double duration = Assert.IsType<double>(completion.Properties["DurationMs"]);
        Assert.True(double.IsFinite(duration) && duration >= 0);
        Assert.Null(completion.Exception);
        return completion;
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
        Assert.Equal("Compra registrada: R$ 58,90 - Mercado", sentTelegramMessage.Text);

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
            Compra compra1 = Assert.IsType<CompraCreationResult.Success>(Compra.Criar(58.90, "Almoco", Categoria.Mercado, januaryDate)).Compra;
            Compra compra2 = Assert.IsType<CompraCreationResult.Success>(Compra.Criar(20.00, "Lanche", Categoria.Lanches, januaryDate.AddDays(1))).Compra;
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
        int otherId;

        using (IServiceScope serviceScope = financeBotApiFactory.Services.CreateScope())
        {
            FinanceBotDbContext financeBotDbContext = serviceScope.ServiceProvider.GetRequiredService<
                FinanceBotDbContext>();
            Compra compra = Assert.IsType<CompraCreationResult.Success>(Compra.Criar(33.50, "Uber para trabalho", Categoria.Uber, DateTime.UtcNow)).Compra;
            Compra other = Assert.IsType<CompraCreationResult.Success>(Compra.Criar(10, "Preservada", Categoria.Mercado, DateTime.UtcNow)).Compra;
            financeBotDbContext.Compras.AddRange(compra, other);
            financeBotDbContext.SaveChanges();
            compraId = compra.Id;
            otherId = other.Id;
        }

        TelegramWebhookRequest webhookRequest = new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest
            {
                Chat = new TelegramChatRequest
                {
                    Id = 998877
                },
                Text = $"  /DELETAR\t00{compraId}  "
            }
        };

        HttpResponseMessage httpResponseMessage = await httpClient.PostAsJsonAsync(
            "/telegram/webhook",
            webhookRequest);

        Assert.Equal(HttpStatusCode.OK, httpResponseMessage.StatusCode);
        SentTelegramMessage sentTelegramMessage = Assert.Single(
            financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages);
        Assert.Equal(998877, sentTelegramMessage.ChatId);
        Assert.Equal("Compra removida: R$ 33,50 - Uber para trabalho (Uber)", sentTelegramMessage.Text);

        using HttpResponseMessage repeatedResponse = await httpClient.PostAsJsonAsync(
            "/telegram/webhook", webhookRequest);
        Assert.Equal(HttpStatusCode.OK, repeatedResponse.StatusCode);
        Assert.Equal(2, financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages.Count);
        Assert.Equal("ID nao encontrado. Nada foi removido.",
            financeBotApiFactory.RecordingTelegramMessageHandler.SentMessages.Last().Text);

        using IServiceScope verifyScope = financeBotApiFactory.Services.CreateScope();
        FinanceBotDbContext verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<
            FinanceBotDbContext>();
        Compra preserved = Assert.Single(verifyDbContext.Compras.ToList());
        Assert.Equal(otherId, preserved.Id);
        Assert.Equal("Preservada", preserved.Descricao);
        Assert.Equal(10, preserved.Valor);
        Assert.Equal(Categoria.Mercado, preserved.Categoria);
    }

    [Theory]
    [InlineData("/deletar -1", "Uso correto: /deletar <ID>. Exemplo: /deletar 42")]
    [InlineData("/deletar 0 extra", "Uso correto: /deletar <ID>. Exemplo: /deletar 42")]
    [InlineData("/deletar 2147483648 extra", "Uso correto: /deletar <ID>. Exemplo: /deletar 42")]
    [InlineData("/deletar@FinanceBot 1", "Uso correto: /deletar <ID>. Exemplo: /deletar 42")]
    [InlineData("/deletar 0", "ID invalido para o comando /deletar.")]
    [InlineData("/deletar 2147483648", "ID invalido para o comando /deletar.")]
    [InlineData("/deletar 2147483647", "ID nao encontrado. Nada foi removido.")]
    public async Task PostWebhook_InvalidOrMissingDeleteId_PreservesExpenseAndSendsExistingMessage(string text, string expected)
    {
        using FinanceBotApiFactory factory = new(998877);
        using HttpClient client = factory.CreateClient();
        int id;
        DateTime date = new(2026, 1, 20, 10, 30, 0, DateTimeKind.Utc);
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>();
            Compra expense = Assert.IsType<CompraCreationResult.Success>(Compra.Criar(33.50, "Preservada", Categoria.Uber, date)).Compra;
            context.Compras.Add(expense);
            await context.SaveChangesAsync();
            id = expense.Id;
        }

        using HttpResponseMessage response = await client.PostAsJsonAsync("/telegram/webhook", new TelegramWebhookRequest
        {
            Message = new TelegramMessageRequest { Chat = new TelegramChatRequest { Id = 998877 }, Text = text }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        SentTelegramMessage reply = Assert.Single(factory.RecordingTelegramMessageHandler.SentMessages);
        Assert.Equal(998877, reply.ChatId);
        Assert.Equal(expected, reply.Text);
        using IServiceScope verify = factory.Services.CreateScope();
        Compra preserved = Assert.Single(verify.ServiceProvider.GetRequiredService<FinanceBotDbContext>().Compras);
        Assert.Equal(id, preserved.Id);
        Assert.Equal(33.50, preserved.Valor);
        Assert.Equal("Preservada", preserved.Descricao);
        Assert.Equal(Categoria.Uber, preserved.Categoria);
        Assert.Equal(date, preserved.Data);
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
        public ConcurrentQueue<string> RequestIds { get; } = new();
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
                services.AddSingleton<IStartupFilter>(new CaptureRequestIdFilter(RequestIds));
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
        public Task AddAsync(Compra compra, CancellationToken cancellationToken) => throw new InvalidOperationException("COMMAND_EXCEPTION_PRIVATE test-token-identifiable");
        public Task<IReadOnlyList<Compra>> ListByPeriodAsync(DateTime periodStartUtc, DateTime periodEndExclusiveUtc, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Compra>>([]);
        public Task<Compra?> FindByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Compra compra, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CaptureRequestIdFilter(ConcurrentQueue<string> identifiers) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, continuation) =>
            {
                context.TraceIdentifier = $"existing-http-{Guid.NewGuid()}";
                identifiers.Enqueue(context.TraceIdentifier);
                return continuation(context);
            });
            next(app);
        };
    }

    private sealed record SentTelegramMessage(long ChatId, string Text);
}
