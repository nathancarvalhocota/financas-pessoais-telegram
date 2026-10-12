using FinanceBot.Api.Commands.Handlers;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class DeleteExpenseCommandHandlerTests
{
    [Theory]
    [InlineData("/deletar 42", 42)]
    [InlineData("/DELETAR\t00042 \r\n", 42)]
    [InlineData("/deletar 2147483647", int.MaxValue)]
    public async Task Handle_ExistingId_ReturnsAllRemovedDataAndDeletesOnlyRequestedExpense(string text, int id)
    {
        var repository = new ExpenseRepository();
        Expense expense = CreateExpense(id);
        Expense other = CreateExpense(1);
        repository.Items.AddRange([expense, other]);
        using var cancellation = new CancellationTokenSource();

        var result = Assert.IsType<DeleteExpenseResult.Success>(await new DeleteExpenseCommandHandler(repository)
            .HandleDeleteExpenseAsync(text, cancellation.Token));

        Assert.Equal(new ExpenseItem(id, 1234.56, "Uber para trabalho", Category.Uber,
            new DateTime(2026, 1, 20, 10, 30, 0, DateTimeKind.Utc)), result.Expense);
        Assert.Equal(DateTimeKind.Utc, result.Expense.DateUtc.Kind);
        Assert.Same(other, Assert.Single(repository.Items));
        Assert.Equal(new[] { "find", "delete" }, repository.Operations);
        Assert.Equal(id, Assert.Single(repository.RequestedIds));
        Assert.Same(expense, Assert.Single(repository.Deleted));
        Assert.All(repository.Tokens, token => Assert.Equal(cancellation.Token, token));
    }

    [Theory]
    [InlineData(42)]
    [InlineData(int.MaxValue)]
    public async Task Handle_MissingId_ReturnsRequestedIdWithoutDeletingOtherExpenses(int id)
    {
        var repository = new ExpenseRepository();
        Expense other = CreateExpense(1);
        repository.Items.Add(other);
        using var cancellation = new CancellationTokenSource();

        var result = Assert.IsType<DeleteExpenseResult.NotFound>(await new DeleteExpenseCommandHandler(repository)
            .HandleDeleteExpenseAsync($"/deletar {id}", cancellation.Token));

        Assert.Equal(id, result.Id);
        Assert.Equal(id, Assert.Single(repository.RequestedIds));
        Assert.Equal(new[] { "find" }, repository.Operations);
        Assert.Equal(cancellation.Token, Assert.Single(repository.Tokens));
        Assert.Same(other, Assert.Single(repository.Items));
        Assert.Empty(repository.Deleted);
    }

    [Theory]
    [InlineData("/deletar")]
    [InlineData("/deletar -1")]
    [InlineData("/deletar +1")]
    [InlineData("/deletar 1.0")]
    [InlineData("/deletar abc")]
    [InlineData("/deletar 1 2")]
    [InlineData("/deletar 0 extra")]
    [InlineData("/deletar 2147483648 extra")]
    [InlineData("/deletar@FinanceBot 42")]
    public async Task Handle_InvalidSyntax_PrecedesIdValidationWithoutIo(string text)
    {
        await AssertInvalidInputWithoutIo<DeleteExpenseError.InvalidSyntax>(text);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("000")]
    [InlineData("2147483648")]
    [InlineData("9999999999999999999999999999999999999999")]
    [InlineData("٤٢")]
    public async Task Handle_UnrepresentableOrNonPositiveId_ReturnsInvalidIdWithoutIo(string id)
    {
        await AssertInvalidInputWithoutIo<DeleteExpenseError.InvalidId>($"/deletar {id}");
    }

    [Theory]
    [InlineData("find", false)]
    [InlineData("delete", false)]
    [InlineData("find", true)]
    [InlineData("delete", true)]
    public async Task Handle_RepositoryFailure_PropagatesIncludingCancellation(string stage, bool canceled)
    {
        using var cancellation = new CancellationTokenSource();
        Exception expected = canceled
            ? new OperationCanceledException(cancellation.Token)
            : new InvalidOperationException("Unexpected repository failure.");
        var repository = new ExpenseRepository { FailStage = stage, Exception = expected };
        Expense expense = CreateExpense(42);
        repository.Items.Add(expense);

        Exception exception = await Assert.ThrowsAnyAsync<Exception>(() => new DeleteExpenseCommandHandler(repository)
            .HandleDeleteExpenseAsync("/deletar 42", cancellation.Token));

        Assert.Same(expected, exception);
        Assert.Equal(stage == "find" ? new[] { "find" } : new[] { "find", "delete" }, repository.Operations);
        Assert.All(repository.Tokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Same(expense, Assert.Single(repository.Items));
        Assert.Empty(repository.Deleted);
    }

    private static async Task AssertInvalidInputWithoutIo<TError>(string text) where TError : DeleteExpenseError
    {
        var repository = new ExpenseRepository();
        Expense expense = CreateExpense(42);
        repository.Items.Add(expense);

        var failure = Assert.IsType<DeleteExpenseResult.Failure>(await new DeleteExpenseCommandHandler(repository)
            .HandleDeleteExpenseAsync(text, CancellationToken.None));

        Assert.IsType<TError>(failure.Error);
        Assert.Empty(repository.Operations);
        Assert.Empty(repository.Deleted);
        Assert.Same(expense, Assert.Single(repository.Items));
    }

    private static Expense CreateExpense(int id)
    {
        Expense expense = Assert.IsType<ExpenseCreationResult.Success>(Expense.Create(1234.56, "Uber para trabalho",
            Category.Uber, new DateTime(2026, 1, 20, 10, 30, 0, DateTimeKind.Utc))).Expense;
        typeof(Expense).GetProperty(nameof(Expense.Id))!.SetValue(expense, id);
        return expense;
    }

    private sealed class ExpenseRepository : IExpenseRepository
    {
        public List<Expense> Items { get; } = new();
        public List<string> Operations { get; } = new();
        public List<CancellationToken> Tokens { get; } = new();
        public List<int> RequestedIds { get; } = new();
        public List<Expense> Deleted { get; } = new();
        public string? FailStage { get; init; }
        public Exception? Exception { get; init; }

        private void Record(string stage, CancellationToken token)
        {
            Operations.Add(stage);
            Tokens.Add(token);
            if (FailStage == stage) throw Exception!;
        }

        public Task<Expense?> FindByIdAsync(int id, CancellationToken cancellationToken)
        {
            Record("find", cancellationToken);
            RequestedIds.Add(id);
            return Task.FromResult(Items.SingleOrDefault(item => item.Id == id));
        }

        public Task DeleteAsync(Expense expense, CancellationToken cancellationToken)
        {
            Record("delete", cancellationToken);
            Assert.True(Items.Remove(expense));
            Deleted.Add(expense);
            return Task.CompletedTask;
        }

        public Task AddAsync(Expense expense, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Expense>> ListByPeriodAsync(DateTime startUtc, DateTime endExclusiveUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
