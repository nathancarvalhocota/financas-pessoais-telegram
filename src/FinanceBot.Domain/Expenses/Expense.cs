using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.Expenses;

public sealed class Expense
{
    private Expense() { }

    private Expense(double amount, string description, Category category, DateTime dateUtc)
    {
        Amount = amount;
        Description = description;
        Category = category;
        DateUtc = dateUtc;
    }

    public int Id { get; private set; }
    public double Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Category Category { get; private set; }
    public DateTime DateUtc { get; private set; }

    public static ExpenseCreationResult Create(double amount, string description, Category category, DateTime dateUtc)
    {
        if (!double.IsFinite(amount) || amount <= 0)
            return new ExpenseCreationResult.Failure(ExpenseError.InvalidAmount);
        if (string.IsNullOrWhiteSpace(description))
            return new ExpenseCreationResult.Failure(ExpenseError.MissingDescription);
        if (!Enum.IsDefined(category))
            return new ExpenseCreationResult.Failure(ExpenseError.InvalidCategory);

        return new ExpenseCreationResult.Success(new Expense(amount, description, category, dateUtc));
    }
}
