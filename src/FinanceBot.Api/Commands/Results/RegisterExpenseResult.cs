using FinanceBot.Domain.Billing;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;

namespace FinanceBot.Api.Commands.Results;

public abstract record RegisterExpenseResult
{
    private RegisterExpenseResult() { }
    public sealed record Success(ExpenseItem Expense, CategoryLimitProgress? Progress, SpendingPaceAlert? Alert) : RegisterExpenseResult;
    public sealed record Failure(RegisterExpenseError Error) : RegisterExpenseResult;
}

public abstract record RegisterExpenseError
{
    private RegisterExpenseError() { }
    public sealed record InvalidSyntax : RegisterExpenseError;
    public sealed record InvalidAmount : RegisterExpenseError;
    public sealed record MissingDescription : RegisterExpenseError;
    public sealed record UnknownCategory(string Text) : RegisterExpenseError;
}

public sealed record SpendingPaceAlert(
    SpendingAlertLevel Level, BillingCycle CurrentCycle, BillingCycle PreviousCycle,
    double CurrentTotal, double PreviousTotal, int Percentage, double Difference,
    IReadOnlyList<CategorySpendingComparison> Categories);

public sealed record CategorySpendingComparison(
    Category Category, double CurrentTotal, double PreviousTotal, SpendingComparison Comparison);

public abstract record SpendingComparison
{
    private SpendingComparison() { }
    public sealed record NoSpending : SpendingComparison;
    public sealed record NoHistory : SpendingComparison;
    public sealed record Comparable(int Percentage, double Difference) : SpendingComparison;
}
