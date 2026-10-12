using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Billing;

namespace FinanceBot.Api.Commands.Parsing;

public sealed record ExpenseListQuery(
    DateTime PeriodStartUtc,
    DateTime PeriodEndExclusiveUtc,
    ExpensePeriod Period,
    CategoryFilter CategoryFilter);

public abstract record ExpenseListQueryParseResult
{
    private ExpenseListQueryParseResult() { }

    public sealed record Success(ExpenseListQuery Query) : ExpenseListQueryParseResult;
    public sealed record Failure(ListExpensesError Error) : ExpenseListQueryParseResult;
}

public static class ExpenseListQueryParser
{
    private static readonly Regex DateRangeRegex = new Regex(
        "^(?<start>\\d{2}/\\d{2}/(?:\\d{2}|\\d{4}))\\s*-\\s*(?<end>\\d{2}/\\d{2}/(?:\\d{2}|\\d{4}))(?:\\s+(?<filter>.+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DatePrefixRegex = new Regex(
        "^\\d{2}/\\d{2}/",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MonthYearShapeRegex = new Regex(
        "^\\d{2}/\\d{2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DateRegex = new Regex(
        "^(\\d{2})/(\\d{2})/(\\d{2}|\\d{4})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ExpenseListQueryParseResult Parse(
        string argumentsText,
        DateTimeOffset messageDateUtc)
    {
        Match dateRangeMatch = DateRangeRegex.Match(argumentsText);
        if (dateRangeMatch.Success)
        {
            return ParseDateRange(dateRangeMatch);
        }

        if (DatePrefixRegex.IsMatch(argumentsText))
        {
            return new ExpenseListQueryParseResult.Failure(
                new ListExpensesError.InvalidDate());
        }

        return ParseBillingCycle(argumentsText, messageDateUtc);
    }

    private static ExpenseListQueryParseResult ParseDateRange(Match dateRangeMatch)
    {
        if (!TryParseDate(dateRangeMatch.Groups["start"].Value, out DateOnly rangeStart) ||
            !TryParseDate(dateRangeMatch.Groups["end"].Value, out DateOnly rangeEnd))
        {
            return new ExpenseListQueryParseResult.Failure(
                new ListExpensesError.InvalidDate());
        }

        if (rangeStart > rangeEnd)
        {
            return new ExpenseListQueryParseResult.Failure(
                new ListExpensesError.InvertedPeriod());
        }

        string filterText = dateRangeMatch.Groups["filter"].Value.Trim();
        CategoryFilterParseResult filterResult = ParseCategoryFilter(filterText);
        if (!filterResult.IsSuccess)
        {
            return new ExpenseListQueryParseResult.Failure(filterResult.Error!);
        }

        var (periodStartUtc, periodEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(rangeStart, rangeEnd);
        return new ExpenseListQueryParseResult.Success(new ExpenseListQuery(
            periodStartUtc,
            periodEndExclusiveUtc,
            new ExpensePeriod(ExpensePeriodKind.DateRange, rangeStart, rangeEnd),
            filterResult.Filter!));
    }

    private static ExpenseListQueryParseResult ParseBillingCycle(
        string argumentsText,
        DateTimeOffset messageDateUtc)
    {
        DateOnly referenceDate = DateTimeConverter.GetBusinessDate(messageDateUtc.UtcDateTime);
        string filterText = argumentsText;
        int firstSpaceIndex = argumentsText.IndexOf(' ');
        string firstToken = firstSpaceIndex < 0
            ? argumentsText
            : argumentsText[..firstSpaceIndex];

        if (MonthYearShapeRegex.IsMatch(firstToken))
        {
            if (!TryParseMonthYear(firstToken, out referenceDate))
            {
                return new ExpenseListQueryParseResult.Failure(
                    new ListExpensesError.InvalidMonth());
            }

            filterText = firstSpaceIndex < 0
                ? string.Empty
                : argumentsText[(firstSpaceIndex + 1)..].Trim();
        }

        CategoryFilterParseResult filterResult = ParseCategoryFilter(filterText);
        if (!filterResult.IsSuccess)
        {
            return new ExpenseListQueryParseResult.Failure(filterResult.Error!);
        }

        BillingCycle cycle = BillingCycle.GetBillingCycle(referenceDate);
        var (periodStartUtc, periodEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(cycle);
        return new ExpenseListQueryParseResult.Success(new ExpenseListQuery(
            periodStartUtc,
            periodEndExclusiveUtc,
            new ExpensePeriod(ExpensePeriodKind.BillingCycle, cycle.StartDate, cycle.EndDate),
            filterResult.Filter!));
    }

    private static CategoryFilterParseResult ParseCategoryFilter(string filterText)
    {
        if (filterText.Length == 0)
        {
            return CategoryFilterParseResult.Success(
                new CategoryFilter(CategoryFilterMode.None, Array.Empty<Categoria>()));
        }

        CategoryFilterMode filterMode = CategoryFilterMode.Include;
        string categoriesText = filterText;
        int firstSpaceIndex = filterText.IndexOf(' ');
        string firstToken = firstSpaceIndex < 0
            ? filterText
            : filterText[..firstSpaceIndex];

        if (NormalizeCommandToken(firstToken) == "sem")
        {
            filterMode = CategoryFilterMode.Exclude;
            categoriesText = firstSpaceIndex < 0
                ? string.Empty
                : filterText[(firstSpaceIndex + 1)..].Trim();

            if (categoriesText.Length == 0)
            {
                return CategoryFilterParseResult.Failure(
                    new ListExpensesError.MissingExcludedCategories());
            }
        }

        List<Categoria> categories = new List<Categoria>();
        foreach (string rawCategory in categoriesText.Split(','))
        {
            string categoryText = rawCategory.Trim();
            if (!CategoryCatalog.TryParse(categoryText, out Categoria category))
            {
                return CategoryFilterParseResult.Failure(
                    new ListExpensesError.UnknownCategory(categoryText));
            }

            categories.Add(category);
        }

        return CategoryFilterParseResult.Success(new CategoryFilter(filterMode, categories));
    }

    private static bool TryParseDate(string dateText, out DateOnly localDate)
    {
        localDate = default;
        Match match = DateRegex.Match(dateText);
        if (!match.Success)
        {
            return false;
        }

        int year = match.Groups[3].Value.Length == 2
            ? 2000 + int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
            : int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

        try
        {
            localDate = new DateOnly(
                year,
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool TryParseMonthYear(string monthYearText, out DateOnly referenceDate)
    {
        referenceDate = default;
        Match monthYearMatch = Regex.Match(
            monthYearText,
            "^(0[1-9]|1[0-2])/(\\d{2})$",
            RegexOptions.CultureInvariant);

        if (!monthYearMatch.Success)
        {
            return false;
        }

        int month = int.Parse(monthYearMatch.Groups[1].Value, CultureInfo.InvariantCulture);
        int year = 2000 + int.Parse(monthYearMatch.Groups[2].Value, CultureInfo.InvariantCulture);
        referenceDate = new DateOnly(year, month, 15);
        return true;
    }

    private static string NormalizeCommandToken(string text)
    {
        string normalizedUnicodeText = text.Normalize(NormalizationForm.FormD);
        StringBuilder normalizedBuilder = new StringBuilder();

        foreach (char character in normalizedUnicodeText)
        {
            UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                normalizedBuilder.Append(char.ToLowerInvariant(character));
            }
        }

        return normalizedBuilder.ToString();
    }

    private sealed record CategoryFilterParseResult(CategoryFilter? Filter, ListExpensesError? Error)
    {
        public bool IsSuccess => Filter is not null;

        public static CategoryFilterParseResult Success(CategoryFilter filter)
        {
            return new CategoryFilterParseResult(filter, null);
        }

        public static CategoryFilterParseResult Failure(ListExpensesError error)
        {
            return new CategoryFilterParseResult(null, error);
        }
    }
}
