using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;

namespace FinanceBot.Api.Commands.Results;

public sealed record ExpenseItem(int Id, double Amount, string Description, Category Category, DateTime DateUtc)
{
    public static ExpenseItem From(Expense expense) =>
        new(expense.Id, expense.Amount, expense.Description, expense.Category, expense.DateUtc);
}
