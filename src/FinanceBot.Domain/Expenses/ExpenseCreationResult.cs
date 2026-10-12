namespace FinanceBot.Domain.Expenses;

public enum ExpenseError
{
    InvalidAmount,
    MissingDescription,
    InvalidCategory
}

public abstract record ExpenseCreationResult
{
    private ExpenseCreationResult() { }

    public sealed record Success(Expense Expense) : ExpenseCreationResult;
    public sealed record Failure(ExpenseError Error) : ExpenseCreationResult;
}
