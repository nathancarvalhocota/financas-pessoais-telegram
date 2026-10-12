using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;

namespace FinanceBot.Api.Commands.Results;

public abstract record CategoryLimitResult
{
    private CategoryLimitResult() { }

    public sealed record Queried(Categoria Category, CategoryLimitProgress? Progress) : CategoryLimitResult;
    public sealed record Updated(Categoria Category, double Value) : CategoryLimitResult;
    public sealed record Removed(Categoria Category) : CategoryLimitResult;
    public sealed record Failure(CategoryLimitError Error) : CategoryLimitResult;
}

public abstract record CategoryLimitError
{
    private CategoryLimitError() { }

    public sealed record InvalidSyntax : CategoryLimitError;
    public sealed record UnknownCategory(string Text) : CategoryLimitError;
    public sealed record InvalidLimit : CategoryLimitError;
}
