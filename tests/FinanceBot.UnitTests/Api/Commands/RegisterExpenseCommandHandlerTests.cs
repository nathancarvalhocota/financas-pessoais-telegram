using FinanceBot.Api.Commands.Handlers;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Billing;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Expenses;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class RegisterExpenseCommandHandlerTests
{
    private static readonly DateTimeOffset MessageDate = new(2026, 4, 20, 9, 0, 0, TimeSpan.FromHours(-3));
    private static readonly DateTime CurrentStart = new(2026, 4, 15, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime CurrentEnd = new(2026, 5, 15, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PreviousStart = new(2026, 3, 15, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ExcessiveBacktracking_ReturnsSyntaxErrorWithoutAccessingRepositories()
    {
        var repository = new Repositories();
        var handler = new RegisterExpenseCommandHandler(repository, repository);
        string text = "/compra 1, " + new string(' ', 100_000) + "\n";

        var result = Assert.IsType<RegisterExpenseResult.Failure>(await handler.HandleRegisterExpenseAsync(
            text, MessageDate, CancellationToken.None));

        Assert.IsType<RegisterExpenseError.InvalidSyntax>(result.Error);
        Assert.Empty(repository.Operations);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Handle_WithoutLimitOrHistory_ReturnsPersistedExpenseDataInUtc()
    {
        var repository = new Repositories();
        var handler = new RegisterExpenseCommandHandler(repository, repository);

        var result = Assert.IsType<RegisterExpenseResult.Success>(await handler.HandleRegisterExpenseAsync(
            "/compra 58,90,  Almoco  , Mercado", MessageDate, CancellationToken.None));

        Expense saved = Assert.Single(repository.Items);
        Assert.Equal(new ExpenseItem(saved.Id, 58.90, "Almoco", Category.Mercado, MessageDate.UtcDateTime), result.Expense);
        Assert.True(result.Expense.Id > 0);
        Assert.Equal(DateTimeKind.Utc, result.Expense.DateUtc.Kind);
        Assert.Null(result.Progress);
        Assert.Null(result.Alert);
    }

    [Fact]
    public async Task Handle_WithLimitAndAlert_PersistsFirstAndSharesOneCurrentCycleRead()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new Repositories { Limit = CreateLimit(100) };
        repository.Items.Add(CreateExpense(100, Category.Mercado, PreviousStart));
        repository.Items.Add(CreateExpense(20, Category.Uber, CurrentStart));
        var handler = new RegisterExpenseCommandHandler(repository, repository);

        var result = Assert.IsType<RegisterExpenseResult.Success>(await handler.HandleRegisterExpenseAsync(
            "/compra 130, Nova, Mercado", MessageDate, cancellation.Token));

        Assert.Equal(new[] { "add", "limit", "current", "previous" }, repository.Operations);
        Assert.Equal(new[] { (CurrentStart, CurrentEnd), (PreviousStart, CurrentStart) }, repository.Periods);
        Assert.All(repository.Tokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Equal(new CategoryLimitProgress(Category.Mercado, 100, 130, 130, true), result.Progress);
        SpendingPaceAlert alert = Assert.IsType<SpendingPaceAlert>(result.Alert);
        Assert.Equal(SpendingAlertLevel.Danger, alert.Level);
        Assert.Equal(150, alert.CurrentTotal);
        Assert.Equal(100, alert.PreviousTotal);
        Assert.Equal(150, alert.Percentage);
        Assert.Equal(50, alert.Difference);
        Assert.Equal(CategoryCatalog.GetAll(), alert.Categories.Select(item => item.Category));
        Assert.Equal(new SpendingComparison.Comparable(130, 30), alert.Categories.Single(item => item.Category == Category.Mercado).Comparison);
        Assert.IsType<SpendingComparison.NoHistory>(alert.Categories.Single(item => item.Category == Category.Uber).Comparison);
        Assert.IsType<SpendingComparison.NoSpending>(alert.Categories.Single(item => item.Category == Category.Saude).Comparison);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(100.01, true)]
    public async Task Handle_LimitEqualityAndExceeding_ReturnDomainProgress(double amount, bool exceeded)
    {
        var repository = new Repositories { Limit = CreateLimit(100) };

        var result = Assert.IsType<RegisterExpenseResult.Success>(await new RegisterExpenseCommandHandler(repository, repository)
            .HandleRegisterExpenseAsync($"/compra {amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}, Teste, Mercado", MessageDate, CancellationToken.None));

        Assert.Equal(new CategoryLimitProgress(Category.Mercado, 100, amount, 100, exceeded), result.Progress);
    }

    [Theory]
    [InlineData(125, SpendingAlertLevel.Warning)]
    [InlineData(150, SpendingAlertLevel.Danger)]
    [InlineData(200, SpendingAlertLevel.Urgent)]
    public async Task Handle_CrossingSeveralThresholds_ReturnsHighestAndDoesNotRepeat(double amount, SpendingAlertLevel expected)
    {
        var repository = new Repositories();
        repository.Items.Add(CreateExpense(100, Category.Mercado, PreviousStart));
        var handler = new RegisterExpenseCommandHandler(repository, repository);

        var result = Assert.IsType<RegisterExpenseResult.Success>(await handler.HandleRegisterExpenseAsync(
            $"/compra {amount}, Teste, Mercado", MessageDate, CancellationToken.None));
        var later = Assert.IsType<RegisterExpenseResult.Success>(await handler.HandleRegisterExpenseAsync(
            "/compra 1, Depois, Mercado", MessageDate, CancellationToken.None));

        Assert.Equal(expected, Assert.IsType<SpendingPaceAlert>(result.Alert).Level);
        Assert.Null(later.Alert);
    }

    [Theory]
    [InlineData("/compra -1, Teste, Inexistente", typeof(RegisterExpenseError.InvalidSyntax))]
    [InlineData("/compra 0,   , Inexistente", typeof(RegisterExpenseError.InvalidAmount))]
    [InlineData("/compra 1,   , Inexistente", typeof(RegisterExpenseError.MissingDescription))]
    [InlineData("/compra 1, Teste, Inexistente", typeof(RegisterExpenseError.UnknownCategory))]
    public async Task Handle_InvalidInput_ReturnsSpecificErrorWithoutIo(string text, Type errorType)
    {
        var repository = new Repositories();

        var result = Assert.IsType<RegisterExpenseResult.Failure>(await new RegisterExpenseCommandHandler(repository, repository)
            .HandleRegisterExpenseAsync(text, MessageDate, CancellationToken.None));

        Assert.IsType(errorType, result.Error);
        Assert.Empty(repository.Operations);
        Assert.Empty(repository.Items);
    }

    [Theory]
    [InlineData("add", false)]
    [InlineData("limit", true)]
    [InlineData("current", true)]
    [InlineData("previous", true)]
    public async Task Handle_UnexpectedPersistenceOrQueryFailure_PropagatesWithoutReturningInputFailure(string stage, bool persisted)
    {
        var repository = new Repositories { FailStage = stage };
        var handler = new RegisterExpenseCommandHandler(repository, repository);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleRegisterExpenseAsync("/compra 10, Teste, Mercado", MessageDate, CancellationToken.None));

        Assert.Same(repository.Exception, exception);
        Assert.Equal(persisted ? 1 : 0, repository.Items.Count);
        Assert.Equal(stage, repository.Operations[^1]);
    }

    private static Expense CreateExpense(double amount, Category category, DateTime date) =>
        Assert.IsType<ExpenseCreationResult.Success>(Expense.Create(amount, "Base", category, date)).Expense;

    private static CategoryLimit CreateLimit(double amount) =>
        Assert.IsType<CategoryLimitCreationResult.Success>(CategoryLimit.Create(Category.Mercado, amount)).Limit;

    private sealed class Repositories : IExpenseRepository, ICategoryLimitRepository
    {
        public List<Expense> Items { get; } = new();
        public CategoryLimit? Limit { get; init; }
        public List<string> Operations { get; } = new();
        public List<CancellationToken> Tokens { get; } = new();
        public List<(DateTime, DateTime)> Periods { get; } = new();
        public string? FailStage { get; init; }
        public InvalidOperationException Exception { get; } = new("Unexpected repository failure.");

        private void Record(string stage, CancellationToken token)
        {
            Operations.Add(stage);
            Tokens.Add(token);
            if (FailStage == stage) throw Exception;
        }

        public Task AddAsync(Expense expense, CancellationToken cancellationToken)
        {
            Record("add", cancellationToken);
            typeof(Expense).GetProperty(nameof(Expense.Id))!.SetValue(expense, Items.Count + 1);
            Items.Add(expense);
            return Task.CompletedTask;
        }

        public Task<CategoryLimit?> GetByCategoryAsync(Category category, CancellationToken cancellationToken)
        {
            Record("limit", cancellationToken);
            return Task.FromResult(Limit?.Category == category ? Limit : null);
        }

        public Task<IReadOnlyList<Expense>> ListByPeriodAsync(DateTime periodStartUtc, DateTime periodEndExclusiveUtc, CancellationToken cancellationToken)
        {
            Record(periodStartUtc == CurrentStart ? "current" : "previous", cancellationToken);
            Periods.Add((periodStartUtc, periodEndExclusiveUtc));
            return Task.FromResult<IReadOnlyList<Expense>>(Items.Where(item => item.DateUtc >= periodStartUtc && item.DateUtc < periodEndExclusiveUtc).ToList());
        }

        public Task<Expense?> FindByIdAsync(int expenseId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Expense expense, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpsertAsync(CategoryLimit categoryLimit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(CategoryLimit categoryLimit, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
