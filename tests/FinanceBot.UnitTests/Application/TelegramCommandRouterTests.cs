using FinanceBot.Application.Abstractions;
using FinanceBot.Application.UseCases;
using FinanceBot.Domain.Entities;
using Xunit;

namespace FinanceBot.UnitTests.Application;

public sealed class TelegramCommandRouterTests
{
    [Fact]
    public async Task RouteAsync_WithCompraAndUnknownCategory_ReturnsCategoryOptionsMessage()
    {
        InMemoryCompraRepository inMemoryCompraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter telegramCommandRouter = new TelegramCommandRouter(inMemoryCompraRepository);

        string response = await telegramCommandRouter.RouteAsync(
            "/compra 200, teste, teste",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains("Categoria inexistente: teste.", response);
        Assert.Contains("Categorias disponiveis:", response);
        Assert.Contains("Mercado", response);
        Assert.DoesNotContain("Uso correto", response);
    }

    [Fact]
    public async Task RouteAsync_WithCompraMentionAndUnknownCategory_ReturnsCategoryOptionsMessage()
    {
        InMemoryCompraRepository inMemoryCompraRepository = new InMemoryCompraRepository();
        TelegramCommandRouter telegramCommandRouter = new TelegramCommandRouter(inMemoryCompraRepository);

        string response = await telegramCommandRouter.RouteAsync(
            "/compra@FinanceBot 200, teste, teste",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Contains("Categoria inexistente: teste.", response);
        Assert.Contains("Categorias disponiveis:", response);
        Assert.DoesNotContain("Uso correto", response);
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
}
