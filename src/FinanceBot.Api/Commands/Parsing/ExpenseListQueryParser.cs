using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FinanceBot.Api.Commands;
using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Billing;

namespace FinanceBot.Api.Commands.Parsing;

internal enum CategoryFilterMode
{
    None,
    Include,
    Exclude
}

internal sealed record CategoryFilter(CategoryFilterMode Mode, IReadOnlyList<Categoria> Categories)
{
    public bool Includes(Categoria category)
    {
        return Mode switch
        {
            CategoryFilterMode.None => true,
            CategoryFilterMode.Include => Categories.Contains(category),
            CategoryFilterMode.Exclude => !Categories.Contains(category),
            _ => false
        };
    }
}

internal sealed record ExpenseListQuery(
    DateTime PeriodStartUtc,
    DateTime PeriodEndExclusiveUtc,
    string PeriodDescription,
    CategoryFilter CategoryFilter);

internal sealed record ExpenseListQueryParseResult(
    ExpenseListQuery? Query,
    string? ErrorMessage)
{
    public bool IsSuccess => Query is not null;

    public static ExpenseListQueryParseResult Success(ExpenseListQuery query)
    {
        return new ExpenseListQueryParseResult(query, null);
    }

    public static ExpenseListQueryParseResult Failure(string errorMessage)
    {
        return new ExpenseListQueryParseResult(null, errorMessage);
    }
}

internal static class ExpenseListQueryParser
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
            return ExpenseListQueryParseResult.Failure(
                "Data invalida. Use DD/MM/AA ou DD/MM/AAAA. Exemplo: /listar 10/02/26 - 20/02/2026");
        }

        return ParseBillingCycle(argumentsText, messageDateUtc);
    }

    private static ExpenseListQueryParseResult ParseDateRange(Match dateRangeMatch)
    {
        if (!TryParseDate(dateRangeMatch.Groups["start"].Value, out DateOnly rangeStart) ||
            !TryParseDate(dateRangeMatch.Groups["end"].Value, out DateOnly rangeEnd))
        {
            return ExpenseListQueryParseResult.Failure(
                "Data invalida. Use DD/MM/AA ou DD/MM/AAAA. Exemplo: /listar 10/02/26 - 20/02/2026");
        }

        if (rangeStart > rangeEnd)
        {
            return ExpenseListQueryParseResult.Failure(
                "Data inicial nao pode ser posterior a data final.");
        }

        string filterText = dateRangeMatch.Groups["filter"].Value.Trim();
        CategoryFilterParseResult filterResult = ParseCategoryFilter(filterText);
        if (!filterResult.IsSuccess)
        {
            return ExpenseListQueryParseResult.Failure(filterResult.ErrorMessage!);
        }

        var (periodStartUtc, periodEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(rangeStart, rangeEnd);
        return ExpenseListQueryParseResult.Success(new ExpenseListQuery(
            periodStartUtc,
            periodEndExclusiveUtc,
            $"{rangeStart:dd/MM/yyyy} a {rangeEnd:dd/MM/yyyy}",
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
                return ExpenseListQueryParseResult.Failure(
                    "Mes/ano invalido. Use o formato MM/YY, exemplo: 09/26");
            }

            filterText = firstSpaceIndex < 0
                ? string.Empty
                : argumentsText[(firstSpaceIndex + 1)..].Trim();
        }

        CategoryFilterParseResult filterResult = ParseCategoryFilter(filterText);
        if (!filterResult.IsSuccess)
        {
            return ExpenseListQueryParseResult.Failure(filterResult.ErrorMessage!);
        }

        BillingCycle cycle = BillingCycle.GetBillingCycle(referenceDate);
        var (periodStartUtc, periodEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(cycle);
        return ExpenseListQueryParseResult.Success(new ExpenseListQuery(
            periodStartUtc,
            periodEndExclusiveUtc,
            $"{cycle.StartDate:dd/MM} a {cycle.EndDate:dd/MM}",
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
                    "Informe ao menos uma categoria após 'sem'. Exemplo: /listar sem Mercado");
            }
        }

        List<Categoria> categories = new List<Categoria>();
        foreach (string rawCategory in categoriesText.Split(','))
        {
            string categoryText = rawCategory.Trim();
            if (!CategoryCatalog.TryParse(categoryText, out Categoria category))
            {
                return CategoryFilterParseResult.Failure(
                    CategoryCommandMessages.InvalidCategory(categoryText));
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

    private sealed record CategoryFilterParseResult(CategoryFilter? Filter, string? ErrorMessage)
    {
        public bool IsSuccess => Filter is not null;

        public static CategoryFilterParseResult Success(CategoryFilter filter)
        {
            return new CategoryFilterParseResult(filter, null);
        }

        public static CategoryFilterParseResult Failure(string errorMessage)
        {
            return new CategoryFilterParseResult(null, errorMessage);
        }
    }
}
