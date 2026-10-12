using FinanceBot.Domain.Expenses;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Api.Commands;
using FinanceBot.Api.Commands.Handlers;
using FinanceBot.Domain.Categories;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class TelegramCommandRouterTests
{
    [Fact]
    public async Task RouteAsync_WithPurchaseAndUnknownCategory_ReturnsCategoryOptionsMessage()
    {
        InMemoryCompraRepository inMemoryCompraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter telegramCommandRouter = new TelegramCommandRouter(
            inMemoryCompraRepository,
            new InMemoryLimiteCategoriaRepository());

        string response = await telegramCommandRouter.RouteMessageAsync(
            "/compra 200, teste, teste",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains("inexistente", response);
        Assert.Contains("Categorias disponiveis", response);
        Assert.Contains("Mercado", response);
        Assert.DoesNotContain("Uso correto", response);
    }

    [Fact]
    public async Task RouteAsync_WithPurchaseMentionAndUnknownCategory_ReturnsCategoryOptionsMessage()
    {
        InMemoryCompraRepository inMemoryCompraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter telegramCommandRouter = new TelegramCommandRouter(
            inMemoryCompraRepository,
            new InMemoryLimiteCategoriaRepository());

        string response = await telegramCommandRouter.RouteMessageAsync(
            "/compra@FinanceBot 200, teste, teste",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains("inexistente", response);
        Assert.Contains("Categorias disponiveis", response);
        Assert.DoesNotContain("Uso correto", response);
    }

    [Fact]
    public async Task RouteAsync_WithLimitDefined_ReturnsConfirmationWithValue()
    {
        InMemoryLimiteCategoriaRepository limiteRepository = new InMemoryLimiteCategoriaRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            limiteRepository);

        string response = await router.RouteMessageAsync(
            "/limite Mercado 800",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains("Limite de Mercado definido", response);
        Assert.Contains("R$ 800,00", response);
    }

    [Fact]
    public async Task RouteAsync_WithLimitZero_RemovesLimit()
    {
        InMemoryLimiteCategoriaRepository limiteRepository = new InMemoryLimiteCategoriaRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            limiteRepository);

        // Define primeiro
        await router.RouteMessageAsync("/limite Mercado 800", DateTimeOffset.UtcNow, CancellationToken.None);

        // Remove
        string response = await router.RouteMessageAsync(
            "/limite Mercado 0",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains("Limite de Mercado removido", response);
        LimiteCategoria? limite = await limiteRepository.GetByCategoryAsync(
            Categoria.Mercado,
            CancellationToken.None);
        Assert.Null(limite);
    }

    [Fact]
    public async Task RouteAsync_WithLimitInvalidCategory_ReturnsError()
    {
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteMessageAsync(
            "/limite categoriaInexistente 100",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains("inexistente", response);
        Assert.Contains("Categorias disponiveis", response);
    }

    [Fact]
    public async Task RouteAsync_WithPurchaseAndLimit_ShowsProgressInResponse()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        InMemoryLimiteCategoriaRepository limiteRepository = new InMemoryLimiteCategoriaRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(compraRepository, limiteRepository);

        // Define limite
        await router.RouteMessageAsync("/limite Mercado 800", DateTimeOffset.UtcNow, CancellationToken.None);

        // Registra compra de R$ 340
        string response = await router.RouteMessageAsync(
            "/compra 340, Pao, Mercado",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains("Compra registrada", response);
        Assert.Contains("Mercado: R$ 340,00 de R$ 800,00", response);
        Assert.Contains("42%", response);
    }

    [Fact]
    public async Task RouteAsync_WithPurchaseExceedingLimit_ShowsAlert()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        InMemoryLimiteCategoriaRepository limiteRepository = new InMemoryLimiteCategoriaRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(compraRepository, limiteRepository);

        DateTimeOffset now = DateTimeOffset.UtcNow;

        // Define limite R$ 800
        await router.RouteMessageAsync("/limite Mercado 800", now, CancellationToken.None);

        // Registra compra prévia de R$ 770
        await router.RouteMessageAsync("/compra 770, Compras anteriores, Mercado", now, CancellationToken.None);

        // Registra compra que ultrapassa (770 + 50 = 820 > 800)
        string response = await router.RouteMessageAsync(
            "/compra 50, Extra, Mercado",
            now,
            CancellationToken.None);

        Assert.Contains("Compra registrada", response);
        Assert.Contains("⚠ Limite de Mercado ultrapassado", response);
        Assert.Contains("R$ 820,00 de R$ 800,00", response);
    }

    [Fact]
    public async Task RouteAsync_WithPurchaseWithoutLimit_DoesNotShowProgress()
    {
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteMessageAsync(
            "/compra 50, Cafe, Mercado",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains("Compra registrada: R$ 50,00 - Mercado", response);
        Assert.DoesNotContain("de R$", response);
        Assert.DoesNotContain("⚠", response);
    }

    private static readonly DateTimeOffset AbrilDeVinteSeis =
        new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RouteAsync_ListInclusion_ShowsOnlyGivenCategories()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        await router.RouteMessageAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);
        await router.RouteMessageAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/listar 04/26 Mercado",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("Compras de 15/04 a 14/05 (Mercado):", response);
        Assert.Contains("Pao", response);
        Assert.DoesNotContain("Cinema", response);
    }

    [Fact]
    public async Task RouteAsync_ListInclusionWithoutDate_UsesCurrentMonthAndFilters()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        await router.RouteMessageAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);
        await router.RouteMessageAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/listar Mercado",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("Pao", response);
        Assert.DoesNotContain("Cinema", response);
    }

    [Fact]
    public async Task RouteAsync_ListInvalidCategory_ReturnsError()
    {
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteMessageAsync(
            "/listar 04/26 categoriaInexistente",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("inexistente", response);
        Assert.Contains("Categorias disponiveis", response);
    }

    [Fact]
    public async Task RouteAsync_ListInclusionWithoutResults_ReturnsFilteredMessage()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        await router.RouteMessageAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/listar 04/26 Lazer",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Equal("Nenhuma compra de Lazer de 15/04 a 14/05.", response);
    }

    [Fact]
    public async Task RouteAsync_ListExclusion_OmitsGivenCategories()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        await router.RouteMessageAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);
        await router.RouteMessageAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/listar 04/26 sem Lazer",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("Compras de 15/04 a 14/05 (sem Lazer):", response);
        Assert.Contains("Pao", response);
        Assert.DoesNotContain("Cinema", response);
    }

    [Fact]
    public async Task RouteAsync_ListExclusionWithoutCategory_ReturnsError()
    {
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteMessageAsync(
            "/listar 04/26 sem",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("Informe ao menos uma categoria após 'sem'", response);
    }

    [Fact]
    public async Task RouteAsync_ListExclusionWithoutResults_ReturnsFilteredMessage()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        await router.RouteMessageAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/listar 04/26 sem Lazer",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Equal("Nenhuma compra de 15/04 a 14/05 (sem Lazer).", response);
    }

    [Fact]
    public async Task RouteAsync_Info_DocumentsListFilter()
    {
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteMessageAsync(
            "/info",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("/listar 04/26 Mercado, Uber", response);
        Assert.Contains("sem Lazer", response);
        Assert.Contains("/listar 10/02/26 - 20/02/2026", response);
    }

    [Fact]
    public async Task RouteAsync_ListWithoutFilter_KeepsHeaderAndListsAll()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        await router.RouteMessageAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);
        await router.RouteMessageAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/listar 04/26",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("Compras de 15/04 a 14/05:", response);
        Assert.Contains("Pao", response);
        Assert.Contains("Cinema", response);
        Assert.Contains("Total: R$ 150,00", response);
    }

    [Fact]
    public async Task RouteAsync_ListWithExplicitDateRange_ReturnsOnlyPurchasesInsideInclusiveRange()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        await router.RouteMessageAsync("/compra 10, Primeiro, Mercado", new DateTimeOffset(2026, 2, 10, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);
        await router.RouteMessageAsync("/compra 20, Segundo, Uber", new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);
        await router.RouteMessageAsync("/compra 30, Fora, Lazer", new DateTimeOffset(2026, 2, 21, 12, 0, 0, TimeSpan.Zero), CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/listar 10/02/26 - 20/02/2026",
            new DateTimeOffset(2026, 2, 20, 12, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Contains("Compras de 10/02/2026 a 20/02/2026:", response);
        Assert.Contains("Primeiro", response);
        Assert.Contains("Segundo", response);
        Assert.DoesNotContain("Fora", response);
        Assert.Contains("Total: R$ 30,00", response);
    }

    [Fact]
    public async Task RouteAsync_ListWithInvertedDateRange_ReturnsSpecificError()
    {
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteMessageAsync(
            "/listar 20/02/26 - 10/02/2026",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Equal("Data inicial nao pode ser posterior a data final.", response);
    }

    [Fact]
    public async Task RouteAsync_WhenCycleTotalCrossesWarning_ReturnsAlertForAllCategories()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        DateTimeOffset previousCycleDate = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset currentCycleDate = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        await router.RouteMessageAsync("/compra 60, Mercado anterior, Mercado", previousCycleDate, CancellationToken.None);
        await router.RouteMessageAsync("/compra 40, Uber anterior, Uber", previousCycleDate, CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/compra 130, Mercado atual, Mercado",
            currentCycleDate,
            CancellationToken.None);

        Assert.Contains("ALERTA AMARELO", response);
        Assert.Contains("Gasto atual: 130% do ciclo anterior (+R$ 30,00).", response);
        Assert.Contains("• Mercado: R$ 130,00 vs R$ 60,00 (217%, +R$ 70,00)", response);
        Assert.Contains("• Educacao: R$ 0,00 vs R$ 0,00 (sem gastos nos dois períodos)", response);
        Assert.Contains("• Uber: R$ 0,00 vs R$ 40,00 (0%, -R$ 40,00)", response);
    }

    [Fact]
    public async Task RouteAsync_WhenCycleTotalCrossesDanger_DoesNotRepeatItOnLaterPurchases()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        DateTimeOffset previousCycleDate = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset currentCycleDate = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        await router.RouteMessageAsync("/compra 100, Base anterior, Mercado", previousCycleDate, CancellationToken.None);
        string dangerResponse = await router.RouteMessageAsync("/compra 150, Danger, Mercado", currentCycleDate, CancellationToken.None);
        string laterResponse = await router.RouteMessageAsync("/compra 10, Depois, Mercado", currentCycleDate, CancellationToken.None);

        Assert.Contains("ALERTA VERMELHO", dangerResponse);
        Assert.DoesNotContain("ALERTA", laterResponse);
    }

    [Fact]
    public async Task RouteAsync_WhenCycleTotalCrossesTwoHundredPercent_ReturnsUrgentAlert()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        DateTimeOffset previousCycleDate = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset currentCycleDate = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        await router.RouteMessageAsync("/compra 100, Base anterior, Mercado", previousCycleDate, CancellationToken.None);
        await router.RouteMessageAsync("/compra 150, Alerta vermelho, Mercado", currentCycleDate, CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/compra 50, Alerta urgente, Mercado",
            currentCycleDate,
            CancellationToken.None);

        Assert.Contains("ALERTA URGENTE", response);
        Assert.Contains("Gasto atual: 200% do ciclo anterior (+R$ 100,00).", response);
    }

    [Fact]
    public async Task RouteAsync_PurchaseAtElevenPmInSaoPauloOnTheFourteenth_RemainsInCurrentCycle()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        DateTimeOffset elevenPmInSaoPauloAsUtc = new DateTimeOffset(2026, 7, 15, 2, 0, 0, TimeSpan.Zero);
        await router.RouteMessageAsync(
            "/compra 120, Uber mes, Uber",
            elevenPmInSaoPauloAsUtc,
            CancellationToken.None);

        string response = await router.RouteMessageAsync(
            "/listar 06/26",
            elevenPmInSaoPauloAsUtc,
            CancellationToken.None);

        Assert.Contains("Compras de 15/06 a 14/07:", response);
        Assert.Contains("Uber mes", response);
    }

    [Theory]
    [InlineData("/listar 16/02/2019 - 16/02/2019", "2019-02-16T02:00:00Z", "2019-02-17T03:00:00Z", "16/02/2019 a 16/02/2019")]
    [InlineData("/listar 28/02/2024 - 29/02/2024", "2024-02-28T03:00:00Z", "2024-03-01T03:00:00Z", "28/02/2024 a 29/02/2024")]
    [InlineData("/listar 31/12/2025 - 01/01/2026", "2025-12-31T03:00:00Z", "2026-01-02T03:00:00Z", "31/12/2025 a 01/01/2026")]
    public async Task RouteAsync_ListDateRange_QueriesCompleteCalendarDaysAndPreservesOrder(
        string command, string startText, string endExclusiveText, string description)
    {
        DateTime startUtc = DateTimeOffset.Parse(startText).UtcDateTime;
        DateTime endExclusiveUtc = DateTimeOffset.Parse(endExclusiveText).UtcDateTime;
        InMemoryCompraRepository repository = new InMemoryCompraRepository();
        await SeedPeriodBoundariesAsync(repository, startUtc, endExclusiveUtc);
        TelegramCommandRouter router = new TelegramCommandRouter(repository, new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteMessageAsync(command, AbrilDeVinteSeis, CancellationToken.None);

        Assert.Equal((startUtc, endExclusiveUtc), Assert.Single(repository.QueriedPeriods));
        Assert.Contains($"Compras de {description}:", response);
        Assert.Contains("Inicio incluido", response);
        Assert.Contains("Ultimo instante", response);
        Assert.DoesNotContain("Antes do inicio", response);
        Assert.DoesNotContain("Fim excluido", response);
        Assert.Contains("Total: R$ 30,00", response);
        Assert.True(response.IndexOf("Ultimo instante", StringComparison.Ordinal) < response.IndexOf("Inicio incluido", StringComparison.Ordinal));
        Assert.Contains($"Ultimo instante · {endExclusiveUtc.AddTicks(-1):dd/MM HH:mm}", response);
    }

    [Theory]
    [InlineData("2026-01-15T02:59:59Z", "2025-12-15T03:00:00Z", "2026-01-15T03:00:00Z", "15/12 a 14/01")]
    [InlineData("2026-01-15T03:00:00Z", "2026-01-15T03:00:00Z", "2026-02-15T03:00:00Z", "15/01 a 14/02")]
    [InlineData("2019-02-17T03:00:00Z", "2019-02-15T02:00:00Z", "2019-03-15T03:00:00Z", "15/02 a 14/03")]
    public async Task RouteAsync_ListWithoutDates_UsesMessageBusinessDateAndExclusiveNextCycle(
        string messageDateText, string startText, string endExclusiveText, string description)
    {
        DateTime startUtc = DateTimeOffset.Parse(startText).UtcDateTime;
        DateTime endExclusiveUtc = DateTimeOffset.Parse(endExclusiveText).UtcDateTime;
        InMemoryCompraRepository repository = new InMemoryCompraRepository();
        await SeedPeriodBoundariesAsync(repository, startUtc, endExclusiveUtc);
        TelegramCommandRouter router = new TelegramCommandRouter(repository, new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteMessageAsync("/listar", DateTimeOffset.Parse(messageDateText), CancellationToken.None);

        Assert.Equal((startUtc, endExclusiveUtc), Assert.Single(repository.QueriedPeriods));
        Assert.Contains($"Compras de {description}:", response);
        Assert.Contains("Total: R$ 30,00", response);
        Assert.DoesNotContain("Antes do inicio", response);
        Assert.DoesNotContain("Fim excluido", response);
    }

    [Fact]
    public async Task RouteAsync_Register_UsesMessageCycleForLimitAndPreviousCycleAlert()
    {
        DateTime startUtc = new DateTime(2019, 2, 15, 2, 0, 0, DateTimeKind.Utc);
        DateTime endExclusiveUtc = new DateTime(2019, 3, 15, 3, 0, 0, DateTimeKind.Utc);
        DateTime previousStartUtc = new DateTime(2019, 1, 15, 2, 0, 0, DateTimeKind.Utc);
        DateTimeOffset messageDate = new DateTimeOffset(2019, 2, 17, 3, 0, 0, TimeSpan.Zero);
        InMemoryCompraRepository repository = new InMemoryCompraRepository();
        await repository.AddAsync(new Compra { Valor = 10, Descricao = "Inicio atual", Categoria = Categoria.Mercado, Data = startUtc }, CancellationToken.None);
        await repository.AddAsync(new Compra { Valor = 20, Descricao = "Fim atual", Categoria = Categoria.Mercado, Data = endExclusiveUtc.AddTicks(-1) }, CancellationToken.None);
        await repository.AddAsync(new Compra { Valor = 999, Descricao = "Proximo ciclo", Categoria = Categoria.Mercado, Data = endExclusiveUtc }, CancellationToken.None);
        await repository.AddAsync(new Compra { Valor = 40, Descricao = "Inicio anterior", Categoria = Categoria.Mercado, Data = previousStartUtc }, CancellationToken.None);
        await repository.AddAsync(new Compra { Valor = 60, Descricao = "Fim anterior", Categoria = Categoria.Mercado, Data = startUtc.AddTicks(-1) }, CancellationToken.None);
        await repository.AddAsync(new Compra { Valor = 999, Descricao = "Antes do ciclo anterior", Categoria = Categoria.Mercado, Data = previousStartUtc.AddTicks(-1) }, CancellationToken.None);
        InMemoryLimiteCategoriaRepository limits = new InMemoryLimiteCategoriaRepository();
        await limits.UpsertAsync(new LimiteCategoria { Categoria = Categoria.Mercado, Valor = 1000 }, CancellationToken.None);
        TelegramCommandRouter router = new TelegramCommandRouter(repository, limits);

        string response = await router.RouteMessageAsync("/compra 100, Nova compra, Mercado", messageDate, CancellationToken.None);

        Assert.Contains("Mercado: R$ 130,00 de R$ 1.000,00 (13%)", response);
        Assert.Contains("ALERTA AMARELO", response);
        Assert.Contains("Ciclo atual (15/02/2019 a 14/03/2019): R$ 130,00", response);
        Assert.Contains("Ciclo anterior (15/01/2019 a 14/02/2019): R$ 100,00", response);
        Assert.Contains("Gasto atual: 130% do ciclo anterior (+R$ 30,00).", response);
        Assert.Equal(new[] { (startUtc, endExclusiveUtc), (startUtc, endExclusiveUtc), (previousStartUtc, startUtc) }, repository.QueriedPeriods);
        Compra saved = Assert.Single(repository.Compras, compra => compra.Descricao == "Nova compra");
        Assert.Equal(messageDate.UtcDateTime, saved.Data);
    }

    [Theory]
    [InlineData("2026-01-15T02:59:59Z", "2025-12-15T03:00:00Z", "2026-01-15T03:00:00Z")]
    [InlineData("2026-01-15T03:00:00Z", "2026-01-15T03:00:00Z", "2026-02-15T03:00:00Z")]
    public async Task RouteAsync_QueryLimit_UsesTimeProviderCycleWithExclusiveEnd(
        string nowText, string startText, string endExclusiveText)
    {
        DateTime startUtc = DateTimeOffset.Parse(startText).UtcDateTime;
        DateTime endExclusiveUtc = DateTimeOffset.Parse(endExclusiveText).UtcDateTime;
        InMemoryCompraRepository repository = new InMemoryCompraRepository();
        await SeedPeriodBoundariesAsync(repository, startUtc, endExclusiveUtc);
        await repository.AddAsync(new Compra { Valor = 500, Descricao = "Outra categoria", Categoria = Categoria.Uber, Data = startUtc }, CancellationToken.None);
        InMemoryLimiteCategoriaRepository limits = new InMemoryLimiteCategoriaRepository();
        await limits.UpsertAsync(new LimiteCategoria { Categoria = Categoria.Mercado, Valor = 100 }, CancellationToken.None);
        TelegramCommandRouter router = new TelegramCommandRouter(
            new RegisterExpenseCommandHandler(repository, limits),
            new ListExpensesCommandHandler(repository),
            new DeleteExpenseCommandHandler(repository),
            new CategoryLimitCommandHandler(repository, limits, new FixedTimeProvider(DateTimeOffset.Parse(nowText))));

        string response = await router.RouteMessageAsync("/limite Mercado", AbrilDeVinteSeis, CancellationToken.None);

        Assert.Equal((startUtc, endExclusiveUtc), Assert.Single(repository.QueriedPeriods));
        Assert.Equal("Mercado: R$ 30,00 de R$ 100,00 (30%)", response);
    }

    private static async Task SeedPeriodBoundariesAsync(InMemoryCompraRepository repository, DateTime startUtc, DateTime endExclusiveUtc)
    {
        await repository.AddAsync(new Compra { Valor = 999, Descricao = "Antes do inicio", Categoria = Categoria.Mercado, Data = startUtc.AddTicks(-1) }, CancellationToken.None);
        await repository.AddAsync(new Compra { Valor = 10, Descricao = "Inicio incluido", Categoria = Categoria.Mercado, Data = startUtc }, CancellationToken.None);
        await repository.AddAsync(new Compra { Valor = 20, Descricao = "Ultimo instante", Categoria = Categoria.Mercado, Data = endExclusiveUtc.AddTicks(-1) }, CancellationToken.None);
        await repository.AddAsync(new Compra { Valor = 999, Descricao = "Fim excluido", Categoria = Categoria.Mercado, Data = endExclusiveUtc }, CancellationToken.None);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryCompraRepository : ICompraRepository
    {
        public List<Compra> Compras { get; } = new List<Compra>();
        public List<(DateTime StartUtc, DateTime EndExclusiveUtc)> QueriedPeriods { get; } = new();
        private int _nextId = 1;

        public Task AddAsync(Compra compra, CancellationToken cancellationToken)
        {
            if (compra.Id == 0)
            {
                compra.Id = _nextId++;
            }

            Compras.Add(compra);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Compra>> ListByPeriodAsync(
            DateTime periodStartUtc,
            DateTime periodEndExclusiveUtc,
            CancellationToken cancellationToken)
        {
            QueriedPeriods.Add((periodStartUtc, periodEndExclusiveUtc));
            IReadOnlyList<Compra> compras = Compras
                .Where(compra => compra.Data >= periodStartUtc && compra.Data < periodEndExclusiveUtc)
                .OrderByDescending(compra => compra.Data)
                .ToList();
            return Task.FromResult(compras);
        }

        public Task<Compra?> FindByIdAsync(int compraId, CancellationToken cancellationToken)
        {
            Compra? compra = Compras.FirstOrDefault(entity => entity.Id == compraId);
            return Task.FromResult(compra);
        }

        public Task DeleteAsync(Compra compra, CancellationToken cancellationToken)
        {
            Compras.Remove(compra);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryLimiteCategoriaRepository : ILimiteCategoriaRepository
    {
        private readonly List<LimiteCategoria> _limites = new List<LimiteCategoria>();
        private int _nextId = 1;

        public Task<LimiteCategoria?> GetByCategoryAsync(
            Categoria categoria,
            CancellationToken cancellationToken)
        {
            LimiteCategoria? limite = _limites.FirstOrDefault(l => l.Categoria == categoria);
            return Task.FromResult(limite);
        }

        public Task UpsertAsync(
            LimiteCategoria limiteCategoria,
            CancellationToken cancellationToken)
        {
            LimiteCategoria? existing = _limites.FirstOrDefault(l => l.Categoria == limiteCategoria.Categoria);
            if (existing is null)
            {
                limiteCategoria.Id = _nextId++;
                _limites.Add(limiteCategoria);
            }
            else
            {
                existing.Valor = limiteCategoria.Valor;
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            LimiteCategoria limiteCategoria,
            CancellationToken cancellationToken)
        {
            _limites.Remove(limiteCategoria);
            return Task.CompletedTask;
        }
    }
}
