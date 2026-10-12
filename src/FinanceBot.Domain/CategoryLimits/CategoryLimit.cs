using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.CategoryLimits;

public sealed class CategoryLimit
{
    private CategoryLimit() { }

    private CategoryLimit(Category category, double amount)
    {
        Category = category;
        Amount = amount;
    }

    public int Id { get; private set; }

    public Category Category { get; private set; }

    public double Amount { get; private set; }

    public static CategoryLimitCreationResult Create(Category category, double amount)
    {
        if (!Enum.IsDefined(category))
        {
            return new CategoryLimitCreationResult.Failure(CategoryLimitValidationError.InvalidCategory);
        }

        if (!IsValidValue(amount))
        {
            return new CategoryLimitCreationResult.Failure(CategoryLimitValidationError.InvalidValue);
        }

        return new CategoryLimitCreationResult.Success(new CategoryLimit(category, amount));
    }

    public CategoryLimitValidationError? UpdateAmount(double amount)
    {
        if (!Enum.IsDefined(Category))
        {
            return CategoryLimitValidationError.InvalidCategory;
        }

        if (!IsValidValue(amount))
        {
            return CategoryLimitValidationError.InvalidValue;
        }

        Amount = amount;
        return null;
    }

    public CategoryLimitProgress EvaluateProgress(double totalSpent) => new(
        Category,
        Amount,
        totalSpent,
        (int)Math.Round(totalSpent / Amount * 100),
        totalSpent > Amount);

    private static bool IsValidValue(double amount) => double.IsFinite(amount) && amount > 0;
}
