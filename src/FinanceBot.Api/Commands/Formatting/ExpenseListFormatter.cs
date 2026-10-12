using System.Text;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Formatting;

public static class ExpenseListFormatter
{
    public static string Format(ListExpensesResult result) => result switch
    {
        ListExpensesResult.Success success => FormatSuccess(success),
        ListExpensesResult.Failure failure => FormatError(failure.Error),
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };

    private static string FormatSuccess(ListExpensesResult.Success result)
    {
        if (result.Items.Count == 0)
        {
            return FormatEmptyList(result);
        }

        string headerSuffix = result.Filter.Mode == CategoryFilterMode.None
            ? string.Empty
            : $" ({FormatFilter(result.Filter)})";

        StringBuilder messageBuilder = new StringBuilder();
        messageBuilder.AppendLine($"Compras de {FormatPeriod(result.Period)}{headerSuffix}:");

        foreach (ExpenseItem expense in result.Items)
        {
            string categoryDisplayName = CategoryCatalog.GetDisplayName(expense.Category);
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"[{expense.Id}] {categoryDisplayName} — R$ {BrazilianCurrencyFormatter.FormatAmount(expense.Amount)}");
            messageBuilder.AppendLine($"    {expense.Description} · {expense.DateUtc:dd/MM HH:mm}");
        }

        messageBuilder.Append($"\nTotal: R$ {BrazilianCurrencyFormatter.FormatAmount(result.Total)}");
        return messageBuilder.ToString();
    }

    private static string FormatEmptyList(ListExpensesResult.Success result)
    {
        return result.Filter.Mode switch
        {
            CategoryFilterMode.Include =>
                $"Nenhuma compra de {FormatFilter(result.Filter)} de {FormatPeriod(result.Period)}.",
            CategoryFilterMode.Exclude =>
                $"Nenhuma compra de {FormatPeriod(result.Period)} ({FormatFilter(result.Filter)}).",
            _ => $"Nenhuma compra encontrada de {FormatPeriod(result.Period)}."
        };
    }

    private static string FormatPeriod(ExpensePeriod period) => period.Kind switch
    {
        ExpensePeriodKind.BillingCycle => $"{period.StartDate:dd/MM} a {period.EndDateInclusive:dd/MM}",
        ExpensePeriodKind.DateRange => $"{period.StartDate:dd/MM/yyyy} a {period.EndDateInclusive:dd/MM/yyyy}",
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    private static string FormatError(ListExpensesError error) => error switch
    {
        ListExpensesError.InvalidSyntax => "Uso correto: /listar <MM/YY>. Exemplo: /listar 09/26",
        ListExpensesError.InvalidDate => "Data invalida. Use DD/MM/AA ou DD/MM/AAAA. Exemplo: /listar 10/02/26 - 20/02/2026",
        ListExpensesError.InvalidMonth => "Mes/ano invalido. Use o formato MM/YY, exemplo: 09/26",
        ListExpensesError.InvertedPeriod => "Data inicial nao pode ser posterior a data final.",
        ListExpensesError.MissingExcludedCategories => "Informe ao menos uma categoria após 'sem'. Exemplo: /listar sem Mercado",
        ListExpensesError.UnknownCategory unknown => CategoryCommandMessages.InvalidCategory(unknown.Text),
        _ => throw new ArgumentOutOfRangeException(nameof(error))
    };

    private static string FormatFilter(CategoryFilter filter)
    {
        string categoryNames = string.Join(", ", filter.Categories.Select(CategoryCatalog.GetDisplayName));
        return filter.Mode == CategoryFilterMode.Exclude ? $"sem {categoryNames}" : categoryNames;
    }
}
