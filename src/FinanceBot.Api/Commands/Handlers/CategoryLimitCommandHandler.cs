using System.Globalization;
using System.Text.RegularExpressions;
using FinanceBot.Api.Commands;
using FinanceBot.Api.Commands.Results;
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

    public async Task<CategoryLimitResult> HandleSetCategoryLimitAsync(string messageText, CancellationToken cancellationToken)
    {
        Match getLimitMatch = GetCategoryLimitCommandRegex.Match(messageText);
        if (getLimitMatch.Success && !SetCategoryLimitCommandRegex.IsMatch(messageText))
        {
            return await HandleGetAsync(getLimitMatch, cancellationToken);
        }

        Match setLimitMatch = SetCategoryLimitCommandRegex.Match(messageText);
        if (!setLimitMatch.Success)
        {
            return new CategoryLimitResult.Failure(new CategoryLimitError.InvalidSyntax());
        }

        string categoryText = setLimitMatch.Groups[1].Value.Trim();
        string valueText = setLimitMatch.Groups[2].Value;

        if (!CategoryCatalog.TryParse(categoryText, out Categoria category))
        {
            return new CategoryLimitResult.Failure(new CategoryLimitError.UnknownCategory(categoryText));
        }

        bool parsedValue = double.TryParse(
            valueText.Replace(',', '.'),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out double value);

        if (!parsedValue)
        {
            return new CategoryLimitResult.Failure(new CategoryLimitError.InvalidLimit());
        }

        if (value == 0)
        {
            LimiteCategoria? existingLimit = await _categoryLimitRepository
                .GetByCategoryAsync(category, cancellationToken);

            if (existingLimit is not null)
            {
                await _categoryLimitRepository.DeleteAsync(existingLimit, cancellationToken);
            }

            return new CategoryLimitResult.Removed(category);
        }

        LimiteCategoriaCreationResult creation = LimiteCategoria.Criar(category, value);
        if (creation is LimiteCategoriaCreationResult.Failure failure)
        {
            CategoryLimitError error = failure.Error switch
            {
                LimiteCategoriaError.InvalidCategory => new CategoryLimitError.UnknownCategory(categoryText),
                LimiteCategoriaError.InvalidValue => new CategoryLimitError.InvalidLimit(),
                _ => throw new ArgumentOutOfRangeException(nameof(failure.Error))
            };
            return new CategoryLimitResult.Failure(error);
        }

        LimiteCategoria categoryLimit = ((LimiteCategoriaCreationResult.Success)creation).Limite;
        await _categoryLimitRepository.UpsertAsync(categoryLimit, cancellationToken);
        return new CategoryLimitResult.Updated(category, categoryLimit.Valor);
    }

    private async Task<CategoryLimitResult> HandleGetAsync(Match getLimitMatch, CancellationToken cancellationToken)
    {
        string categoryText = getLimitMatch.Groups[1].Value.Trim();
        if (!CategoryCatalog.TryParse(categoryText, out Categoria category))
        {
            return new CategoryLimitResult.Failure(new CategoryLimitError.UnknownCategory(categoryText));
        }

        LimiteCategoria? categoryLimit = await _categoryLimitRepository
            .GetByCategoryAsync(category, cancellationToken);

        if (categoryLimit is null)
        {
            return new CategoryLimitResult.Queried(category, null);
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var (periodStartUtc, periodEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(
            DateTimeConverter.GetBusinessDate(nowUtc));

        IReadOnlyList<Compra> cycleExpenses = await _expenseRepository.ListByPeriodAsync(
            periodStartUtc,
            periodEndExclusiveUtc,
            cancellationToken);

        double totalSpending = cycleExpenses
            .Where(expense => expense.Categoria == category)
            .Sum(expense => expense.Valor);

        return new CategoryLimitResult.Queried(category, categoryLimit.AvaliarProgresso(totalSpending));
    }
}
