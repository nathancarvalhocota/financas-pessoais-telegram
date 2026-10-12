namespace FinanceBot.Api.Commands.Results;

public abstract record DeleteExpenseResult
{
    private DeleteExpenseResult() { }

    public sealed record Success(ExpenseItem Expense) : DeleteExpenseResult;
    public sealed record NotFound(int Id) : DeleteExpenseResult;
    public sealed record Failure(DeleteExpenseError Error) : DeleteExpenseResult;
}

public abstract record DeleteExpenseError
{
    private DeleteExpenseError() { }

    public sealed record InvalidSyntax : DeleteExpenseError;
    public sealed record InvalidId : DeleteExpenseError;
}
