using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceBot.IntegrationTests.Infrastructure;

public sealed class CompraRepositoryTests
{
    [Fact]
    public async Task ListByPeriodAsync_IncludesStartExcludesEndAndOrdersNewestFirst()
    {
        DbContextOptions<FinanceBotDbContext> options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using FinanceBotDbContext context = new FinanceBotDbContext(options);
        DateTime startUtc = new DateTime(2025, 12, 15, 3, 0, 0, DateTimeKind.Utc);
        DateTime endExclusiveUtc = new DateTime(2026, 1, 15, 3, 0, 0, DateTimeKind.Utc);
        Compra beforeStart = ExpenseAt(startUtc.AddTicks(-1));
        Compra atStart = ExpenseAt(startUtc);
        Compra lastInstant = ExpenseAt(endExclusiveUtc.AddTicks(-1));
        Compra atEnd = ExpenseAt(endExclusiveUtc);
        context.Compras.AddRange(atStart, atEnd, beforeStart, lastInstant);
        await context.SaveChangesAsync();
        CompraRepository repository = new CompraRepository(context);

        IReadOnlyList<Compra> expenses = await repository.ListByPeriodAsync(startUtc, endExclusiveUtc, CancellationToken.None);

        Assert.Equal(new[] { lastInstant.Id, atStart.Id }, expenses.Select(expense => expense.Id));
        Assert.All(expenses, expense => Assert.Equal(DateTimeKind.Utc, expense.Data.Kind));
    }

    private static Compra ExpenseAt(DateTime dateUtc) => new Compra
    {
        Valor = 10,
        Descricao = "Compra na fronteira",
        Categoria = Categoria.Mercado,
        Data = dateUtc
    };
}
