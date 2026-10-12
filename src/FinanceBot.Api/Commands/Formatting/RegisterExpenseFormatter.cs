using System.Text;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Billing;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Formatting;

public static class RegisterExpenseFormatter
{
    public static string Format(RegisterExpenseResult result) => result switch
    {
        RegisterExpenseResult.Failure failure => FormatError(failure.Error),
        RegisterExpenseResult.Success success => FormatSuccess(success),
        _ => throw new InvalidOperationException("Unknown expense result.")
    };

    private static string FormatError(RegisterExpenseError error) => error switch
    {
        RegisterExpenseError.InvalidSyntax => "Uso correto: /compra <VALOR>, <DESCRICAO>, <CATEGORIA>. " +
            "Exemplo: /compra 58,90, Almoco, Mercado",
        RegisterExpenseError.InvalidAmount => "Valor invalido. Use um numero positivo, exemplo: 58,90",
        RegisterExpenseError.MissingDescription => "Descricao obrigatoria no comando /compra.",
        RegisterExpenseError.UnknownCategory unknown => CategoryCommandMessages.InvalidCategory(unknown.Text),
        _ => throw new InvalidOperationException("Unknown expense error.")
    };

    private static string FormatSuccess(RegisterExpenseResult.Success result)
    {
        string category = CategoryCatalog.GetDisplayName(result.Expense.Category);
        string response = $"Compra registrada: R$ {BrazilianCurrencyFormatter.FormatAmount(result.Expense.Amount)} - {category}";
        if (result.Progress is { } progress)
        {
            string spending = BrazilianCurrencyFormatter.FormatAmount(progress.TotalSpent);
            string limit = BrazilianCurrencyFormatter.FormatAmount(progress.Limit);
            response += progress.IsExceeded
                ? $"\n⚠ Limite de {category} ultrapassado: R$ {spending} de R$ {limit}"
                : $"\n{category}: R$ {spending} de R$ {limit} ({progress.Percentage}%)";
        }

        return result.Alert is { } alert ? $"{response}\n\n{FormatAlert(alert)}" : response;
    }

    private static string FormatAlert(SpendingPaceAlert alert)
    {
        var text = new StringBuilder();
        text.AppendLine(alert.Level switch
        {
            SpendingAlertLevel.Urgent => "🚨 ALERTA URGENTE: gasto do ciclo o dobro do anterior",
            SpendingAlertLevel.Danger => "🚨 ALERTA VERMELHO: gasto do ciclo muito acima do anterior",
            _ => "⚠️ ALERTA AMARELO: gasto do ciclo acima do anterior"
        });
        text.AppendLine();
        text.AppendLine($"Ciclo atual ({alert.CurrentCycle.StartDate:dd/MM/yyyy} a {alert.CurrentCycle.EndDate:dd/MM/yyyy}): R$ {BrazilianCurrencyFormatter.FormatAmount(alert.CurrentTotal)}");
        text.AppendLine($"Ciclo anterior ({alert.PreviousCycle.StartDate:dd/MM/yyyy} a {alert.PreviousCycle.EndDate:dd/MM/yyyy}): R$ {BrazilianCurrencyFormatter.FormatAmount(alert.PreviousTotal)}");
        text.AppendLine($"Gasto atual: {alert.Percentage}% do ciclo anterior ({BrazilianCurrencyFormatter.FormatSignedAmount(alert.Difference)}).");
        text.AppendLine();
        text.AppendLine("Categorias:");
        foreach (CategorySpendingComparison category in alert.Categories)
        {
            text.Append($"• {CategoryCatalog.GetDisplayName(category.Category)}: R$ {BrazilianCurrencyFormatter.FormatAmount(category.CurrentTotal)} vs R$ {BrazilianCurrencyFormatter.FormatAmount(category.PreviousTotal)} ");
            text.AppendLine(category.Comparison switch
            {
                SpendingComparison.NoSpending => "(sem gastos nos dois períodos)",
                SpendingComparison.NoHistory => "(sem histórico comparável)",
                SpendingComparison.Comparable comparable => $"({comparable.Percentage}%, {BrazilianCurrencyFormatter.FormatSignedAmount(comparable.Difference)})",
                _ => throw new InvalidOperationException("Unknown spending comparison.")
            });
        }
        return text.ToString().TrimEnd();
    }
}
