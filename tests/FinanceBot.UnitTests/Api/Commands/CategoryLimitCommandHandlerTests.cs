using FinanceBot.Api.Commands.Handlers;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Expenses;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class CategoryLimitCommandHandlerTests
{
    [Fact]
    public async Task Handle_QueryWithoutLimit_IsNormalAndDoesNotQueryExpenses()
    {
        var expenses = new ExpenseRepository();
        var handler = CreateHandler(expenses, new LimitRepository());

        var result = Assert.IsType<CategoryLimitResult.Queried>(await handler.HandleSetCategoryLimitAsync("/limite Mercado", CancellationToken.None));

        Assert.Equal(Categoria.Mercado, result.Category);
        Assert.Null(result.Progress);
        Assert.Empty(expenses.Periods);
    }

    [Theory]
    [InlineData(100, 100, false)]
    [InlineData(125, 125, true)]
    public async Task Handle_QueryWithLimit_UsesClockAndReturnsCategoryProgress(double spending, int percentage, bool exceeded)
    {
        DateTime start = new DateTime(2025, 12, 15, 3, 0, 0, DateTimeKind.Utc);
        DateTime end = new DateTime(2026, 1, 15, 3, 0, 0, DateTimeKind.Utc);
        var expenses = new ExpenseRepository();
        expenses.Items.AddRange(new[]
        {
            Expense(Categoria.Mercado, spending, start),
            Expense(Categoria.Uber, 500, start),
            Expense(Categoria.Mercado, 999, start.AddTicks(-1)),
            Expense(Categoria.Mercado, 999, end)
        });
        var limits = new LimitRepository();
        await limits.UpsertAsync(CreateLimit(100), CancellationToken.None);
        var handler = CreateHandler(expenses, limits);

        var result = Assert.IsType<CategoryLimitResult.Queried>(await handler.HandleSetCategoryLimitAsync("/limite Mercado", CancellationToken.None));

        Assert.Equal(new CategoryLimitProgress(Categoria.Mercado, 100, spending, percentage, exceeded), result.Progress);
        Assert.Equal((start, end), Assert.Single(expenses.Periods));
    }

    [Theory]
    [InlineData("/limite Mercado 800", 800)]
    [InlineData("/limite@FinanceBot mÉrcado 800,25", 800.25)]
    public async Task Handle_Update_ReturnsUpdatedData(string command, double expectedValue)
    {
        var expenses = new ExpenseRepository();
        var limits = new LimitRepository();
        LimiteCategoria existing = CreateLimit(100);
        await limits.UpsertAsync(existing, CancellationToken.None);
        var handler = CreateHandler(expenses, limits);

        var result = Assert.IsType<CategoryLimitResult.Updated>(await handler.HandleSetCategoryLimitAsync(command, CancellationToken.None));

        Assert.Equal(Categoria.Mercado, result.Category);
        Assert.Equal(expectedValue, result.Value);
        Assert.Same(existing, limits.Limit);
        Assert.Equal(expectedValue, existing.Valor);
        Assert.Empty(expenses.Periods);
    }

    [Fact]
    public async Task Handle_DefineWithoutLimit_PersistsValidEntity()
    {
        var limits = new LimitRepository();
        var handler = CreateHandler(new ExpenseRepository(), limits);

        var result = Assert.IsType<CategoryLimitResult.Updated>(await handler.HandleSetCategoryLimitAsync("/limite Mercado 800", CancellationToken.None));

        Assert.Equal(800, result.Value);
        Assert.Equal(800, limits.Limit!.Valor);
        Assert.Equal(1, limits.Upserts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_Zero_RemovesExistingLimitAndConfirmsEvenIfAbsent(bool exists)
    {
        var limits = new LimitRepository();
        if (exists) await limits.UpsertAsync(CreateLimit(100), CancellationToken.None);
        int upsertsBefore = limits.Upserts;
        var expenses = new ExpenseRepository();
        var handler = CreateHandler(expenses, limits);

        var result = Assert.IsType<CategoryLimitResult.Removed>(await handler.HandleSetCategoryLimitAsync("/limite Mercado 0", CancellationToken.None));

        Assert.Equal(Categoria.Mercado, result.Category);
        Assert.Null(limits.Limit);
        Assert.Equal(exists ? 1 : 0, limits.Deletes);
        Assert.Equal(upsertsBefore, limits.Upserts);
        Assert.Empty(expenses.Periods);
    }

    [Theory]
    [InlineData("/limite")]
    [InlineData("/limite Inexistente -1")]
    [InlineData("/limite Mercado NaN")]
    [InlineData("/limite Mercado Infinity")]
    [InlineData("/limite Mercado -1")]
    [InlineData("/limite Mercado 10.001")]
    public async Task Handle_InvalidSyntax_ReturnsTypedErrorWithoutRepositoryAccess(string command)
    {
        var limits = new LimitRepository();
        var handler = CreateHandler(new ExpenseRepository(), limits);

        var failure = Assert.IsType<CategoryLimitResult.Failure>(await handler.HandleSetCategoryLimitAsync(command, CancellationToken.None));

        Assert.IsType<CategoryLimitError.InvalidSyntax>(failure.Error);
        Assert.Equal(0, limits.Reads + limits.Upserts + limits.Deletes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_UnknownCategory_PrecedesOverflowValue(bool overflow)
    {
        var limits = new LimitRepository();
        var handler = CreateHandler(new ExpenseRepository(), limits);
        string command = overflow ? "/limite Desconhecida " + new string('9', 400) : "/limite Desconhecida";

        var failure = Assert.IsType<CategoryLimitResult.Failure>(await handler.HandleSetCategoryLimitAsync(command, CancellationToken.None));

        Assert.Equal(new CategoryLimitError.UnknownCategory("Desconhecida"), failure.Error);
        Assert.Equal(0, limits.Reads + limits.Upserts + limits.Deletes);
    }

    [Fact]
    public async Task Handle_OverflowValue_TranslatesDomainErrorAndPreservesExistingLimit()
    {
        var limits = new LimitRepository();
        await limits.UpsertAsync(CreateLimit(100), CancellationToken.None);
        var handler = CreateHandler(new ExpenseRepository(), limits);

        var failure = Assert.IsType<CategoryLimitResult.Failure>(await handler.HandleSetCategoryLimitAsync("/limite Mercado " + new string('9', 400), CancellationToken.None));

        Assert.IsType<CategoryLimitError.InvalidLimit>(failure.Error);
        Assert.Equal(100, limits.Limit!.Valor);
        Assert.Equal(1, limits.Upserts);
    }

    private static CategoryLimitCommandHandler CreateHandler(ExpenseRepository expenses, LimitRepository limits) =>
        new(expenses, limits, new FixedTimeProvider());

    private static LimiteCategoria CreateLimit(double value) =>
        Assert.IsType<LimiteCategoriaCreationResult.Success>(LimiteCategoria.Criar(Categoria.Mercado, value)).Limite;

    private static Compra Expense(Categoria category, double value, DateTime date) =>
        new() { Categoria = category, Valor = value, Descricao = "Compra", Data = date };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 1, 15, 2, 59, 59, TimeSpan.Zero);
    }

    private sealed class LimitRepository : ILimiteCategoriaRepository
    {
        public LimiteCategoria? Limit { get; private set; }
        public int Reads { get; private set; }
        public int Upserts { get; private set; }
        public int Deletes { get; private set; }

        public Task<LimiteCategoria?> GetByCategoryAsync(Categoria categoria, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult(Limit?.Categoria == categoria ? Limit : null);
        }

        public Task UpsertAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken)
        {
            Upserts++;
            if (Limit is null) Limit = limiteCategoria;
            else Assert.Null(Limit.AlterarValor(limiteCategoria.Valor));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken)
        {
            Deletes++;
            Assert.Same(Limit, limiteCategoria);
            Limit = null;
            return Task.CompletedTask;
        }
    }

    private sealed class ExpenseRepository : ICompraRepository
    {
        public List<Compra> Items { get; } = new();
        public List<(DateTime Start, DateTime EndExclusive)> Periods { get; } = new();

        public Task<IReadOnlyList<Compra>> ListByPeriodAsync(DateTime periodStartUtc, DateTime periodEndExclusiveUtc, CancellationToken cancellationToken)
        {
            Periods.Add((periodStartUtc, periodEndExclusiveUtc));
            return Task.FromResult<IReadOnlyList<Compra>>(Items.Where(item => item.Data >= periodStartUtc && item.Data < periodEndExclusiveUtc).ToList());
        }

        public Task AddAsync(Compra compra, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Compra?> FindByIdAsync(int compraId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Compra compra, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
