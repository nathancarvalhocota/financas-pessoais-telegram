namespace FinanceBot.Domain.CategoryLimits;

public enum CategoryLimitValidationError
{
    InvalidCategory,
    InvalidValue
}

public abstract record CategoryLimitCreationResult
{
    private CategoryLimitCreationResult() { }

    public sealed record Success(CategoryLimit Limit) : CategoryLimitCreationResult;
    public sealed record Failure(CategoryLimitValidationError Error) : CategoryLimitCreationResult;
}
