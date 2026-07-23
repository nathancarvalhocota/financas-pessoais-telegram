using System.Text;
using FinanceBot.Application.Commands.Parsing;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;

namespace FinanceBot.Application.Commands.Formatting;

internal static class ExpenseListFormatter
{
    public static string Format(ExpenseListQuery query, IReadOnlyList<Compra> expenses)
    {
        IReadOnlyList<Compra> filteredExpenses = expenses
            .Where(expense => query.CategoryFilter.Includes(expense.Categoria))
            .ToList();

        if (filteredExpenses.Count == 0)
        {
            return FormatEmptyList(query);
        }

        string headerSuffix = query.CategoryFilter.Mode == CategoryFilterMode.None
            ? string.Empty
            : $" ({FormatFilter(query.CategoryFilter)})";

        StringBuilder messageBuilder = new StringBuilder();
        messageBuilder.AppendLine($"Compras de {query.PeriodDescription}{headerSuffix}:");

        double totalValue = 0;
        foreach (Compra expense in filteredExpenses)
        {
            totalValue += expense.Valor;
            string categoryDisplayName = CategoryCatalog.GetDisplayName(expense.Categoria);
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"[{expense.Id}] {categoryDisplayName} — R$ {BrazilianCurrencyFormatter.FormatAmount(expense.Valor)}");
            messageBuilder.AppendLine($"    {expense.Descricao} · {expense.Data:dd/MM HH:mm}");
        }

        messageBuilder.Append($"\nTotal: R$ {BrazilianCurrencyFormatter.FormatAmount(totalValue)}");
        return messageBuilder.ToString();
    }

    private static string FormatEmptyList(ExpenseListQuery query)
    {
        return query.CategoryFilter.Mode switch
        {
            CategoryFilterMode.Include =>
                $"Nenhuma compra de {FormatFilter(query.CategoryFilter)} de {query.PeriodDescription}.",
            CategoryFilterMode.Exclude =>
                $"Nenhuma compra de {query.PeriodDescription} ({FormatFilter(query.CategoryFilter)}).",
            _ => $"Nenhuma compra encontrada de {query.PeriodDescription}."
        };
    }

    private static string FormatFilter(CategoryFilter filter)
    {
        string categoryNames = string.Join(", ", filter.Categories.Select(CategoryCatalog.GetDisplayName));
        return filter.Mode == CategoryFilterMode.Exclude ? $"sem {categoryNames}" : categoryNames;
    }
}
