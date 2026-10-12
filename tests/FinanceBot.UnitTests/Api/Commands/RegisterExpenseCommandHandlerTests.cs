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
    public async Task Handle_WithoutLimitOrHistory_ReturnsPersistedExpenseDataInUtc()
    {
        var repository = new Repositories();
        var handler = new RegisterExpenseCommandHandler(repository, repository);

        var result = Assert.IsType<RegisterExpenseResult.Success>(await handler.HandleRegisterExpenseAsync(
            "/compra 58,90,  Almoco  , Mercado", MessageDate, CancellationToken.None));

        Compra saved = Assert.Single(repository.Items);
        Assert.Equal(new ExpenseItem(saved.Id, 58.90, "Almoco", Categoria.Mercado, MessageDate.UtcDateTime), result.Expense);
        Assert.True(result.Expense.Id > 0);
        Assert.Equal(DateTimeKind.Utc, result.Expense.DateUtc.Kind);
        Assert.Null(result.Progress);
        Assert.Null(result.Alert);
    }

    [Fact]
    public async Task Handle_WithLimitAndAlert_PersistsFirstAndSharesOneCurrentCycleRead()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new Repositories { Limit = Limit(100) };
        repository.Items.Add(Expense(100, Categoria.Mercado, PreviousStart));
        repository.Items.Add(Expense(20, Categoria.Uber, CurrentStart));
        var handler = new RegisterExpenseCommandHandler(repository, repository);

        var result = Assert.IsType<RegisterExpenseResult.Success>(await handler.HandleRegisterExpenseAsync(
            "/compra 130, Nova, Mercado", MessageDate, cancellation.Token));

        Assert.Equal(new[] { "add", "limit", "current", "previous" }, repository.Operations);
        Assert.Equal(new[] { (CurrentStart, CurrentEnd), (PreviousStart, CurrentStart) }, repository.Periods);
        Assert.All(repository.Tokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Equal(new CategoryLimitProgress(Categoria.Mercado, 100, 130, 130, true), result.Progress);
        SpendingPaceAlert alert = Assert.IsType<SpendingPaceAlert>(result.Alert);
        Assert.Equal(SpendingAlertLevel.Danger, alert.Level);
        Assert.Equal(150, alert.CurrentTotal);
        Assert.Equal(100, alert.PreviousTotal);
        Assert.Equal(150, alert.Percentage);
        Assert.Equal(50, alert.Difference);
        Assert.Equal(CategoryCatalog.GetAll(), alert.Categories.Select(item => item.Category));
        Assert.Equal(new SpendingComparison.Comparable(130, 30), alert.Categories.Single(item => item.Category == Categoria.Mercado).Comparison);
        Assert.IsType<SpendingComparison.NoHistory>(alert.Categories.Single(item => item.Category == Categoria.Uber).Comparison);
        Assert.IsType<SpendingComparison.NoSpending>(alert.Categories.Single(item => item.Category == Categoria.Saude).Comparison);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(100.01, true)]
    public async Task Handle_LimitEqualityAndExceeding_ReturnDomainProgress(double amount, bool exceeded)
    {
        var repository = new Repositories { Limit = Limit(100) };

        var result = Assert.IsType<RegisterExpenseResult.Success>(await new RegisterExpenseCommandHandler(repository, repository)
            .HandleRegisterExpenseAsync($"/compra {amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}, Teste, Mercado", MessageDate, CancellationToken.None));

        Assert.Equal(new CategoryLimitProgress(Categoria.Mercado, 100, amount, 100, exceeded), result.Progress);
    }

    [Theory]
    [InlineData(125, SpendingAlertLevel.Warning)]
    [InlineData(150, SpendingAlertLevel.Danger)]
    [InlineData(200, SpendingAlertLevel.Urgent)]
    public async Task Handle_CrossingSeveralThresholds_ReturnsHighestAndDoesNotRepeat(double amount, SpendingAlertLevel expected)
    {
        var repository = new Repositories();
        repository.Items.Add(Expense(100, Categoria.Mercado, PreviousStart));
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
        Assert.Equal(stage, repository.Operations.Last());
    }

    private static Compra Expense(double amount, Categoria category, DateTime date) =>
        Assert.IsType<CompraCreationResult.Success>(Compra.Criar(amount, "Base", category, date)).Compra;

    private static LimiteCategoria Limit(double amount) =>
        Assert.IsType<LimiteCategoriaCreationResult.Success>(LimiteCategoria.Criar(Categoria.Mercado, amount)).Limite;

    private sealed class Repositories : ICompraRepository, ILimiteCategoriaRepository
    {
        public List<Compra> Items { get; } = new();
        public LimiteCategoria? Limit { get; init; }
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

        public Task AddAsync(Compra expense, CancellationToken cancellationToken)
        {
            Record("add", cancellationToken);
            typeof(Compra).GetProperty(nameof(Compra.Id))!.SetValue(expense, Items.Count + 1);
            Items.Add(expense);
            return Task.CompletedTask;
        }

        public Task<LimiteCategoria?> GetByCategoryAsync(Categoria category, CancellationToken cancellationToken)
        {
            Record("limit", cancellationToken);
            return Task.FromResult(Limit?.Categoria == category ? Limit : null);
        }

        public Task<IReadOnlyList<Compra>> ListByPeriodAsync(DateTime startUtc, DateTime endExclusiveUtc, CancellationToken cancellationToken)
        {
            Record(startUtc == CurrentStart ? "current" : "previous", cancellationToken);
            Periods.Add((startUtc, endExclusiveUtc));
            return Task.FromResult<IReadOnlyList<Compra>>(Items.Where(item => item.Data >= startUtc && item.Data < endExclusiveUtc).ToList());
        }

        public Task<Compra?> FindByIdAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(Compra expense, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpsertAsync(LimiteCategoria limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(LimiteCategoria limit, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
