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
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static readonly Regex GetCategoryLimitCommandRegex = new Regex(
        "^/limite(?:@[A-Za-z0-9_]+)?\\s+(\\S+)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private readonly IExpenseRepository _expenseRepository;
    private readonly ICategoryLimitRepository _categoryLimitRepository;
    private readonly TimeProvider _timeProvider;

    public CategoryLimitCommandHandler(
        IExpenseRepository expenseRepository,
        ICategoryLimitRepository categoryLimitRepository,
        TimeProvider timeProvider)
    {
        _expenseRepository = expenseRepository;
        _categoryLimitRepository = categoryLimitRepository;
        _timeProvider = timeProvider;
    }

    public async Task<CategoryLimitResult> HandleSetCategoryLimitAsync(string messageText, CancellationToken cancellationToken)
    {
        Match getLimitMatch;
        Match setLimitMatch;
        try
        {
            getLimitMatch = GetCategoryLimitCommandRegex.Match(messageText);
            setLimitMatch = SetCategoryLimitCommandRegex.Match(messageText);
        }
        catch (RegexMatchTimeoutException)
        {
            return new CategoryLimitResult.Failure(new CategoryLimitError.InvalidSyntax());
        }

        if (getLimitMatch.Success && !setLimitMatch.Success)
        {
            return await HandleGetAsync(getLimitMatch, cancellationToken);
        }

        if (!setLimitMatch.Success)
        {
            return new CategoryLimitResult.Failure(new CategoryLimitError.InvalidSyntax());
        }

        string categoryText = setLimitMatch.Groups[1].Value.Trim();
        string valueText = setLimitMatch.Groups[2].Value;

        if (!CategoryCatalog.TryParse(categoryText, out Category category))
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
            CategoryLimit? existingLimit = await _categoryLimitRepository
                .GetByCategoryAsync(category, cancellationToken);

            if (existingLimit is not null)
            {
                await _categoryLimitRepository.DeleteAsync(existingLimit, cancellationToken);
            }

            return new CategoryLimitResult.Removed(category);
        }

        CategoryLimitCreationResult creation = CategoryLimit.Create(category, value);
        if (creation is CategoryLimitCreationResult.Failure failure)
        {
            CategoryLimitError error = failure.Error switch
            {
                CategoryLimitValidationError.InvalidCategory => new CategoryLimitError.UnknownCategory(categoryText),
                CategoryLimitValidationError.InvalidValue => new CategoryLimitError.InvalidLimit(),
                _ => throw new InvalidOperationException("Unknown category limit domain error.")
            };
            return new CategoryLimitResult.Failure(error);
        }

        CategoryLimit categoryLimit = ((CategoryLimitCreationResult.Success)creation).Limit;
        await _categoryLimitRepository.UpsertAsync(categoryLimit, cancellationToken);
        return new CategoryLimitResult.Updated(category, categoryLimit.Amount);
    }

    private async Task<CategoryLimitResult> HandleGetAsync(Match getLimitMatch, CancellationToken cancellationToken)
    {
        string categoryText = getLimitMatch.Groups[1].Value.Trim();
        if (!CategoryCatalog.TryParse(categoryText, out Category category))
        {
            return new CategoryLimitResult.Failure(new CategoryLimitError.UnknownCategory(categoryText));
        }

        CategoryLimit? categoryLimit = await _categoryLimitRepository
            .GetByCategoryAsync(category, cancellationToken);

        if (categoryLimit is null)
        {
            return new CategoryLimitResult.Queried(category, null);
        }

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var (periodStartUtc, periodEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(
            DateTimeConverter.GetBusinessDate(nowUtc));

        IReadOnlyList<Expense> cycleExpenses = await _expenseRepository.ListByPeriodAsync(
            periodStartUtc,
            periodEndExclusiveUtc,
            cancellationToken);

        double totalSpending = cycleExpenses
            .Where(expense => expense.Category == category)
            .Sum(expense => expense.Amount);

        return new CategoryLimitResult.Queried(category, categoryLimit.EvaluateProgress(totalSpending));
    }
}
