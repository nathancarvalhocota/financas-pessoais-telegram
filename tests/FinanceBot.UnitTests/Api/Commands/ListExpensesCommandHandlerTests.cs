using FinanceBot.Api.Commands.Handlers;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class ListExpensesCommandHandlerTests
{
    private static readonly DateTimeOffset MessageDate = DateTimeOffset.Parse("2026-04-15T03:00:00Z");

    [Theory]
    [InlineData("", CategoryFilterMode.None, 65, new[] { 4, 3, 2, 1 })]
    [InlineData(" Mercado, Uber, Mercado", CategoryFilterMode.Include, 55, new[] { 4, 2, 1 })]
    [InlineData(" sem Lazer, Lazer", CategoryFilterMode.Exclude, 55, new[] { 4, 2, 1 })]
    public async Task Handle_ValidQuery_FiltersAndTotalsOncePreservingRepositoryOrder(
        string suffix, CategoryFilterMode mode, double total, int[] expectedIds)
    {
        var repository = new ExpenseRepository();
        repository.Items.AddRange([
            Expense(4, 25, Categoria.Uber, "2026-05-15T02:59:59Z"),
            Expense(3, 10, Categoria.Lazer, "2026-04-18T14:00:00Z"),
            Expense(2, 20, Categoria.Mercado, "2026-04-16T12:00:00Z"),
            Expense(1, 10, Categoria.Mercado, "2026-04-15T03:00:00Z")]);
        using var cancellation = new CancellationTokenSource();

        var result = Assert.IsType<ListExpensesResult.Success>(await new ListExpensesCommandHandler(repository)
            .HandleListExpensesAsync("/LISTAR\t04/26" + suffix, MessageDate, cancellation.Token));

        Assert.Equal(new ExpensePeriod(ExpensePeriodKind.BillingCycle, new DateOnly(2026, 4, 15), new DateOnly(2026, 5, 14)), result.Period);
        Assert.Equal(mode, result.Filter.Mode);
        Assert.Equal(total, result.Total);
        Assert.Equal(expectedIds, result.Items.Select(item => item.Id));
        Assert.Equal(new ExpenseItem(4, 25, "Compra 4", Categoria.Uber, DateTime.Parse("2026-05-15T02:59:59Z").ToUniversalTime()), result.Items[0]);
        Assert.Equal((DateTimeOffset.Parse("2026-04-15T03:00:00Z").UtcDateTime,
            DateTimeOffset.Parse("2026-05-15T03:00:00Z").UtcDateTime, cancellation.Token), Assert.Single(repository.Queries));
        Assert.Equal(4, repository.Items.Count);
    }

    [Theory]
    [InlineData("/listar", "2026-04-15T02:59:59Z", "2026-03-15T03:00:00Z", "2026-04-15T03:00:00Z")]
    [InlineData("/listar", "2026-04-15T03:00:00Z", "2026-04-15T03:00:00Z", "2026-05-15T03:00:00Z")]
    [InlineData("/listar 16/02/2019 - 16/02/2019", "2026-04-15T03:00:00Z", "2019-02-16T02:00:00Z", "2019-02-17T03:00:00Z")]
    public async Task Handle_Period_PassesExactExclusiveBoundsToRepository(
        string text, string messageDate, string start, string end)
    {
        var repository = new ExpenseRepository();
        DateTime startUtc = DateTimeOffset.Parse(start).UtcDateTime;
        DateTime endUtc = DateTimeOffset.Parse(end).UtcDateTime;
        repository.Items.AddRange([
            Expense(4, 40, Categoria.Mercado, endUtc),
            Expense(3, 30, Categoria.Mercado, endUtc.AddTicks(-1)),
            Expense(2, 20, Categoria.Mercado, startUtc),
            Expense(1, 10, Categoria.Mercado, startUtc.AddTicks(-1))]);

        var result = Assert.IsType<ListExpensesResult.Success>(await new ListExpensesCommandHandler(repository)
            .HandleListExpensesAsync(text, DateTimeOffset.Parse(messageDate), CancellationToken.None));

        Assert.Equal((startUtc, endUtc, CancellationToken.None), Assert.Single(repository.Queries));
        Assert.Equal(new[] { 3, 2 }, result.Items.Select(item => item.Id));
        Assert.Equal(50, result.Total);
    }

    [Theory]
    [InlineData("/listar 04/26", false, CategoryFilterMode.None)]
    [InlineData("/listar 04/26 Lazer", true, CategoryFilterMode.Include)]
    [InlineData("/listar 04/26 sem Mercado", true, CategoryFilterMode.Exclude)]
    public async Task Handle_EmptyList_IsSuccessWithPeriodFilterAndZeroTotal(string text, bool seed, CategoryFilterMode mode)
    {
        var repository = new ExpenseRepository();
        if (seed) repository.Items.Add(Expense(1, 10, Categoria.Mercado, "2026-04-20T12:00:00Z"));

        var result = Assert.IsType<ListExpensesResult.Success>(await new ListExpensesCommandHandler(repository)
            .HandleListExpensesAsync(text, MessageDate, CancellationToken.None));

        Assert.Equal(ExpensePeriodKind.BillingCycle, result.Period.Kind);
        Assert.Equal(mode, result.Filter.Mode);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
        Assert.Single(repository.Queries);
    }

    [Theory]
    [InlineData("/listar@FinanceBot 13/26", typeof(ListExpensesError.InvalidSyntax))]
    [InlineData("/listar\n04/26\nMercado", typeof(ListExpensesError.InvalidSyntax))]
    [InlineData("/listar 31/02/26 - 01/02/26 desconhecida", typeof(ListExpensesError.InvalidDate))]
    [InlineData("/listar 20/02/26 - 10/02/26 sem", typeof(ListExpensesError.InvertedPeriod))]
    [InlineData("/listar 13/26 sem", typeof(ListExpensesError.InvalidMonth))]
    [InlineData("/listar sem", typeof(ListExpensesError.MissingExcludedCategories))]
    [InlineData("/listar Mercado, desconhecida", typeof(ListExpensesError.UnknownCategory))]
    public async Task Handle_InvalidInput_ReturnsTypedFailureWithoutIo(string text, Type errorType)
    {
        var repository = new ExpenseRepository();

        var result = Assert.IsType<ListExpensesResult.Failure>(await new ListExpensesCommandHandler(repository)
            .HandleListExpensesAsync(text, MessageDate, CancellationToken.None));

        Assert.IsType(errorType, result.Error);
        if (result.Error is ListExpensesError.UnknownCategory unknown) Assert.Equal("desconhecida", unknown.Text);
        Assert.Empty(repository.Queries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_RepositoryFailure_PropagatesIncludingCancellation(bool canceled)
    {
        using var cancellation = new CancellationTokenSource();
        Exception expected = canceled ? new OperationCanceledException(cancellation.Token) : new InvalidOperationException("Query failed.");
        var repository = new ExpenseRepository { Exception = expected };

        Exception actual = await Assert.ThrowsAnyAsync<Exception>(() => new ListExpensesCommandHandler(repository)
            .HandleListExpensesAsync("/listar", MessageDate, cancellation.Token));

        Assert.Same(expected, actual);
        Assert.Equal(cancellation.Token, Assert.Single(repository.Queries).Token);
    }

    private static Compra Expense(int id, double amount, Categoria category, string date) =>
        Expense(id, amount, category, DateTimeOffset.Parse(date).UtcDateTime);

    private static Compra Expense(int id, double amount, Categoria category, DateTime date)
    {
        Compra expense = Assert.IsType<CompraCreationResult.Success>(Compra.Criar(amount, $"Compra {id}", category, date)).Compra;
        typeof(Compra).GetProperty(nameof(Compra.Id))!.SetValue(expense, id);
        return expense;
    }

    private sealed class ExpenseRepository : ICompraRepository
    {
        public List<Compra> Items { get; } = new();
        public List<(DateTime Start, DateTime EndExclusive, CancellationToken Token)> Queries { get; } = new();
        public Exception? Exception { get; init; }

        public Task<IReadOnlyList<Compra>> ListByPeriodAsync(DateTime startUtc, DateTime endExclusiveUtc, CancellationToken cancellationToken)
        {
            Queries.Add((startUtc, endExclusiveUtc, cancellationToken));
            if (Exception is not null) throw Exception;
            return Task.FromResult<IReadOnlyList<Compra>>(Items.Where(item => item.Data >= startUtc && item.Data < endExclusiveUtc).ToList());
        }

        public Task AddAsync(Compra expense, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Compra?> FindByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Compra expense, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
