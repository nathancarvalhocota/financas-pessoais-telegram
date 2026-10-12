using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FinanceBot.Api.Commands;
using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Domain.Expenses;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Billing;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Handlers;

public sealed class RegisterExpenseCommandHandler
{
    private static readonly Regex ExpenseCommandRegex = new Regex(
        "^/compra(?:@[A-Za-z0-9_]+)?\\s+([0-9]+(?:[.,][0-9]{1,2})?)\\s*,\\s*(.+?)\\s*,\\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly ICompraRepository _expenseRepository;
    private readonly ILimiteCategoriaRepository _categoryLimitRepository;

    public RegisterExpenseCommandHandler(
        ICompraRepository expenseRepository,
        ILimiteCategoriaRepository categoryLimitRepository)
    {
        _expenseRepository = expenseRepository;
        _categoryLimitRepository = categoryLimitRepository;
    }

    public async Task<string> HandleRegisterExpenseAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        Match commandMatch = ExpenseCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return "Uso correto: /compra <VALOR>, <DESCRICAO>, <CATEGORIA>. " +
                "Exemplo: /compra 58,90, Almoco, Mercado";
        }

        string valueText = commandMatch.Groups[1].Value;
        string descriptionText = commandMatch.Groups[2].Value.Trim();
        string categoryText = commandMatch.Groups[3].Value.Trim();

        bool parsedValue = double.TryParse(
            valueText.Replace(',', '.'),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out double value);

        if (!parsedValue || value <= 0)
        {
            return "Valor invalido. Use um numero positivo, exemplo: 58,90";
        }

        if (string.IsNullOrWhiteSpace(descriptionText))
        {
            return "Descricao obrigatoria no comando /compra.";
        }

        if (!CategoryCatalog.TryParse(categoryText, out Categoria category))
        {
            return CategoryCommandMessages.InvalidCategory(categoryText);
        }

        DateTime expenseDateUtc = messageDateUtc.UtcDateTime;
        Compra expense = new Compra
        {
            Valor = value,
            Descricao = descriptionText,
            Categoria = category,
            Data = expenseDateUtc
        };

        await _expenseRepository.AddAsync(expense, cancellationToken);

        string categoryDisplayName = CategoryCatalog.GetDisplayName(category);
        string response = $"Compra registrada: R$ {BrazilianCurrencyFormatter.FormatAmount(value)} - {categoryDisplayName}";

        LimiteCategoria? categoryLimit = await _categoryLimitRepository
            .GetByCategoryAsync(category, cancellationToken);

        if (categoryLimit is null)
        {
            return await AppendPaceAlertAsync(response, expense, cancellationToken);
        }

        var (periodStartUtc, periodEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(
            DateTimeConverter.GetBusinessDate(expenseDateUtc));

        IReadOnlyList<Compra> cycleExpenses = await _expenseRepository.ListByPeriodAsync(
            periodStartUtc,
            periodEndExclusiveUtc,
            cancellationToken);

        double accumulatedSpending = cycleExpenses
            .Where(item => item.Categoria == category)
            .Sum(item => item.Valor);

        string formattedSpending = BrazilianCurrencyFormatter.FormatAmount(accumulatedSpending);
        string formattedLimit = BrazilianCurrencyFormatter.FormatAmount(categoryLimit.Valor);

        if (categoryLimit.IsExceededBy(accumulatedSpending))
        {
            return await AppendPaceAlertAsync(
                $"{response}\n⚠ Limite de {categoryDisplayName} ultrapassado: R$ {formattedSpending} de R$ {formattedLimit}",
                expense,
                cancellationToken);
        }

        int percentage = categoryLimit.CalculateUsagePercentage(accumulatedSpending);
        return await AppendPaceAlertAsync(
            $"{response}\n{categoryDisplayName}: R$ {formattedSpending} de R$ {formattedLimit} ({percentage}%)",
            expense,
            cancellationToken);
    }

    private async Task<string> AppendPaceAlertAsync(
        string response,
        Compra expense,
        CancellationToken cancellationToken)
    {
        BillingCycle currentCycle = BillingCycle.GetBillingCycle(
            DateTimeConverter.GetBusinessDate(expense.Data));
        var (currentStartUtc, currentEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(currentCycle);

        BillingCycle previousCycle = BillingCycle.GetBillingCycle(currentCycle.StartDate.AddMonths(-1));
        var (previousStartUtc, previousEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(previousCycle);

        IReadOnlyList<Compra> currentExpenses = await _expenseRepository.ListByPeriodAsync(
            currentStartUtc,
            currentEndExclusiveUtc,
            cancellationToken);
        IReadOnlyList<Compra> previousExpenses = await _expenseRepository.ListByPeriodAsync(
            previousStartUtc,
            previousEndExclusiveUtc,
            cancellationToken);

        double currentTotal = currentExpenses.Sum(item => item.Valor);
        double previousTotal = previousExpenses.Sum(item => item.Valor);
        double totalBeforeExpense = Math.Max(0, currentTotal - expense.Valor);
        SpendingAlertLevel? alertLevel = SpendingAlertPolicy.GetCrossedLevel(
            previousTotal,
            totalBeforeExpense,
            currentTotal);

        if (alertLevel is null)
        {
            return response;
        }

        StringBuilder alert = new StringBuilder();
        alert.AppendLine(alertLevel switch
        {
            SpendingAlertLevel.Urgent => "🚨 ALERTA URGENTE: gasto do ciclo o dobro do anterior",
            SpendingAlertLevel.Danger => "🚨 ALERTA VERMELHO: gasto do ciclo muito acima do anterior",
            _ => "⚠️ ALERTA AMARELO: gasto do ciclo acima do anterior"
        });
        alert.AppendLine();
        alert.AppendLine($"Ciclo atual ({currentCycle.StartDate:dd/MM/yyyy} a {currentCycle.EndDate:dd/MM/yyyy}): R$ {BrazilianCurrencyFormatter.FormatAmount(currentTotal)}");
        alert.AppendLine($"Ciclo anterior ({previousCycle.StartDate:dd/MM/yyyy} a {previousCycle.EndDate:dd/MM/yyyy}): R$ {BrazilianCurrencyFormatter.FormatAmount(previousTotal)}");
        alert.AppendLine($"Gasto atual: {(int)Math.Round(currentTotal / previousTotal * 100)}% do ciclo anterior ({BrazilianCurrencyFormatter.FormatSignedAmount(currentTotal - previousTotal)}).");
        alert.AppendLine();
        alert.AppendLine("Categorias:");

        foreach (Categoria category in Enum.GetValues<Categoria>())
        {
            double currentCategoryTotal = currentExpenses
                .Where(item => item.Categoria == category)
                .Sum(item => item.Valor);
            double previousCategoryTotal = previousExpenses
                .Where(item => item.Categoria == category)
                .Sum(item => item.Valor);

            alert.Append($"• {CategoryCatalog.GetDisplayName(category)}: R$ {BrazilianCurrencyFormatter.FormatAmount(currentCategoryTotal)} vs R$ {BrazilianCurrencyFormatter.FormatAmount(previousCategoryTotal)} ");

            if (previousCategoryTotal <= 0 && currentCategoryTotal <= 0)
            {
                alert.Append("(sem gastos nos dois períodos)");
            }
            else if (previousCategoryTotal <= 0)
            {
                alert.Append("(sem histórico comparável)");
            }
            else
            {
                alert.Append($"({(int)Math.Round(currentCategoryTotal / previousCategoryTotal * 100)}%, {BrazilianCurrencyFormatter.FormatSignedAmount(currentCategoryTotal - previousCategoryTotal)})");
            }

            alert.AppendLine();
        }

        return $"{response}\n\n{alert.ToString().TrimEnd()}";
    }
}
