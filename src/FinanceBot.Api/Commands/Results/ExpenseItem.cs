using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;

namespace FinanceBot.Api.Commands.Results;

public sealed record ExpenseItem(int Id, double Amount, string Description, Categoria Category, DateTime DateUtc)
{
    public static ExpenseItem From(Compra expense) =>
        new(expense.Id, expense.Valor, expense.Descricao, expense.Categoria, expense.Data);
}
