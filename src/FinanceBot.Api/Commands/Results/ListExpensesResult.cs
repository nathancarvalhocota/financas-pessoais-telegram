using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Results;

public abstract record ListExpensesResult
{
    private ListExpensesResult() { }

    public sealed record Success(
        ExpensePeriod Period,
        CategoryFilter Filter,
        IReadOnlyList<ExpenseItem> Items,
        double Total) : ListExpensesResult;
    public sealed record Failure(ListExpensesError Error) : ListExpensesResult;
}

public enum ExpensePeriodKind
{
    BillingCycle,
    DateRange
}

public sealed record ExpensePeriod(ExpensePeriodKind Kind, DateOnly StartDate, DateOnly EndDateInclusive);

public enum CategoryFilterMode
{
    None,
    Include,
    Exclude
}

public sealed record CategoryFilter(CategoryFilterMode Mode, IReadOnlyList<Category> Categories);

public abstract record ListExpensesError
{
    private ListExpensesError() { }

    public sealed record InvalidSyntax : ListExpensesError;
    public sealed record InvalidDate : ListExpensesError;
    public sealed record InvalidMonth : ListExpensesError;
    public sealed record InvertedPeriod : ListExpensesError;
    public sealed record MissingExcludedCategories : ListExpensesError;
    public sealed record UnknownCategory(string Text) : ListExpensesError;
}
