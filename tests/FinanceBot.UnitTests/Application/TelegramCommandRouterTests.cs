using FinanceBot.Application.Abstractions;
using FinanceBot.Application.UseCases;
using FinanceBot.Domain.Entities;
using FinanceBot.Domain.Enums;
using Xunit;

namespace FinanceBot.UnitTests.Application;

public sealed class TelegramCommandRouterTests
{
    [Fact]
    public async Task RouteAsync_WithPurchaseAndUnknownCategory_ReturnsCategoryOptionsMessage()
    {
        InMemoryCompraRepository inMemoryCompraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter telegramCommandRouter = new TelegramCommandRouter(
            inMemoryCompraRepository,
            new InMemoryLimiteCategoriaRepository());

        string response = await telegramCommandRouter.RouteAsync(
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

        string response = await telegramCommandRouter.RouteAsync(
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

        string response = await router.RouteAsync(
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
        await router.RouteAsync("/limite Mercado 800", DateTimeOffset.UtcNow, CancellationToken.None);

        // Remove
        string response = await router.RouteAsync(
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

        string response = await router.RouteAsync(
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
        await router.RouteAsync("/limite Mercado 800", DateTimeOffset.UtcNow, CancellationToken.None);

        // Registra compra de R$ 340
        string response = await router.RouteAsync(
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
        await router.RouteAsync("/limite Mercado 800", now, CancellationToken.None);

        // Registra compra prévia de R$ 770
        await router.RouteAsync("/compra 770, Compras anteriores, Mercado", now, CancellationToken.None);

        // Registra compra que ultrapassa (770 + 50 = 820 > 800)
        string response = await router.RouteAsync(
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

        string response = await router.RouteAsync(
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

        await router.RouteAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);
        await router.RouteAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteAsync(
            "/listar 04/26 Mercado",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("Compras de 15/04 a 15/05 (Mercado):", response);
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

        await router.RouteAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);
        await router.RouteAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteAsync(
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

        string response = await router.RouteAsync(
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

        await router.RouteAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteAsync(
            "/listar 04/26 Lazer",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Equal("Nenhuma compra de Lazer de 15/04 a 15/05.", response);
    }

    [Fact]
    public async Task RouteAsync_ListExclusion_OmitsGivenCategories()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        await router.RouteAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);
        await router.RouteAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteAsync(
            "/listar 04/26 sem Lazer",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("Compras de 15/04 a 15/05 (sem Lazer):", response);
        Assert.Contains("Pao", response);
        Assert.DoesNotContain("Cinema", response);
    }

    [Fact]
    public async Task RouteAsync_ListExclusionWithoutCategory_ReturnsError()
    {
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteAsync(
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

        await router.RouteAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteAsync(
            "/listar 04/26 sem Lazer",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Equal("Nenhuma compra de 15/04 a 15/05 (sem Lazer).", response);
    }

    [Fact]
    public async Task RouteAsync_Info_DocumentsListFilter()
    {
        TelegramCommandRouter router = new TelegramCommandRouter(
            new InMemoryCompraRepository(),
            new InMemoryLimiteCategoriaRepository());

        string response = await router.RouteAsync(
            "/info",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("/listar 04/26 Mercado, Uber", response);
        Assert.Contains("sem Lazer", response);
    }

    [Fact]
    public async Task RouteAsync_ListWithoutFilter_KeepsHeaderAndListsAll()
    {
        InMemoryCompraRepository compraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter router = new TelegramCommandRouter(
            compraRepository,
            new InMemoryLimiteCategoriaRepository());

        await router.RouteAsync("/compra 100, Pao, Mercado", AbrilDeVinteSeis, CancellationToken.None);
        await router.RouteAsync("/compra 50, Cinema, Lazer", AbrilDeVinteSeis, CancellationToken.None);

        string response = await router.RouteAsync(
            "/listar 04/26",
            AbrilDeVinteSeis,
            CancellationToken.None);

        Assert.Contains("Compras de 15/04 a 15/05:", response);
        Assert.Contains("Pao", response);
        Assert.Contains("Cinema", response);
        Assert.Contains("Total: R$ 150,00", response);
    }

    private sealed class InMemoryCompraRepository : ICompraRepository
    {
        private List<Compra> Compras { get; } = new List<Compra>();

        public Task AddAsync(Compra compra, CancellationToken cancellationToken)
        {
            Compras.Add(compra);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Compra>> ListByPeriodAsync(
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Compra> compras = Compras;
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
