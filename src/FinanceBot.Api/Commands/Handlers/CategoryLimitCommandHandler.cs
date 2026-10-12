using System.Globalization;
using System.Text.RegularExpressions;
using FinanceBot.Api.Commands;
using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Domain.Expenses;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Handlers;

public sealed class CategoryLimitCommandHandler
{
    private static readonly Regex SetCategoryLimitCommandRegex = new Regex(
        "^/limite(?:@[A-Za-z0-9_]+)?\\s+(\\S+)\\s+([0-9]+(?:[.,][0-9]{1,2})?)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex GetCategoryLimitCommandRegex = new Regex(
        "^/limite(?:@[A-Za-z0-9_]+)?\\s+(\\S+)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly ICompraRepository _expenseRepository;
    private readonly ILimiteCategoriaRepository _categoryLimitRepository;
    private readonly TimeProvider _timeProvider;

    public CategoryLimitCommandHandler(
        ICompraRepository expenseRepository,
        ILimiteCategoriaRepository categoryLimitRepository,
        TimeProvider timeProvider)
    {
        _expenseRepository = expenseRepository;
        _categoryLimitRepository = categoryLimitRepository;
        _timeProvider = timeProvider;
    }

    public async Task<string> HandleSetCategoryLimitAsync(string messageText, CancellationToken cancellationToken)
    {
        Match getLimitMatch = GetCategoryLimitCommandRegex.Match(messageText);
        if (getLimitMatch.Success && !SetCategoryLimitCommandRegex.IsMatch(messageText))
        {
            return await HandleGetAsync(getLimitMatch, cancellationToken);
        }

        Match setLimitMatch = SetCategoryLimitCommandRegex.Match(messageText);
        if (!setLimitMatch.Success)
        {
            return "Uso correto: /limite <CATEGORIA> <VALOR>. Exemplo: /limite Mercado 800\n" +
                "Para consultar: /limite <CATEGORIA>. Exemplo: /limite Mercado";
        }

        string categoryText = setLimitMatch.Groups[1].Value.Trim();
        string valueText = setLimitMatch.Groups[2].Value;

        if (!CategoryCatalog.TryParse(categoryText, out Categoria category))
        {
            return CategoryCommandMessages.InvalidCategory(categoryText);
        }

        bool parsedValue = double.TryParse(
            valueText.Replace(',', '.'),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out double value);

        if (!parsedValue || value < 0)
        {
            return "Valor invalido. Use um numero positivo ou 0 para remover o limite.";
        }

        string categoryDisplayName = CategoryCatalog.GetDisplayName(category);

        if (value == 0)
        {
            LimiteCategoria? existingLimit = await _categoryLimitRepository
                .GetByCategoryAsync(category, cancellationToken);

            if (existingLimit is not null)
            {
                await _categoryLimitRepository.DeleteAsync(existingLimit, cancellationToken);
            }

            return $"Limite de {categoryDisplayName} removido";
        }

        LimiteCategoria categoryLimit = new LimiteCategoria
        {
            Categoria = category,
            Valor = value
        };

        await _categoryLimitRepository.UpsertAsync(categoryLimit, cancellationToken);
        return $"Limite de {categoryDisplayName} definido: R$ {BrazilianCurrencyFormatter.FormatAmount(value)}";
    }

    private async Task<string> HandleGetAsync(Match getLimitMatch, CancellationToken cancellationToken)
    {
        string categoryText = getLimitMatch.Groups[1].Value.Trim();
        if (!CategoryCatalog.TryParse(categoryText, out Categoria category))
        {
            return CategoryCommandMessages.InvalidCategory(categoryText);
        }

        string categoryDisplayName = CategoryCatalog.GetDisplayName(category);
        LimiteCategoria? categoryLimit = await _categoryLimitRepository
            .GetByCategoryAsync(category, cancellationToken);

        if (categoryLimit is null)
        {
            return $"Nenhum limite definido para {categoryDisplayName}.";
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var (periodStartUtc, periodEndUtc) = DateTimeConverter.GetUtcPeriod(
            DateTimeConverter.GetBusinessDate(nowUtc));

        IReadOnlyList<Compra> cycleExpenses = await _expenseRepository.ListByPeriodAsync(
            periodStartUtc,
            periodEndUtc.AddDays(1),
            cancellationToken);

        double totalSpending = cycleExpenses
            .Where(expense => expense.Categoria == category)
            .Sum(expense => expense.Valor);

        int percentage = categoryLimit.CalculateUsagePercentage(totalSpending);
        return $"{categoryDisplayName}: R$ {BrazilianCurrencyFormatter.FormatAmount(totalSpending)} de R$ {BrazilianCurrencyFormatter.FormatAmount(categoryLimit.Valor)} ({percentage}%)";
    }
}
