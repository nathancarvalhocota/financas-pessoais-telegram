using FinanceBot.Api.Commands.Parsing;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class ExpenseListQueryParserTests
{
    private static readonly DateTimeOffset MessageDate = DateTimeOffset.Parse("2026-04-15T02:59:59Z");

    [Theory]
    [InlineData("", "2026-03-15", "2026-04-14", "2026-03-15T03:00:00Z", "2026-04-15T03:00:00Z")]
    [InlineData("04/26", "2026-04-15", "2026-05-14", "2026-04-15T03:00:00Z", "2026-05-15T03:00:00Z")]
    [InlineData("12/25", "2025-12-15", "2026-01-14", "2025-12-15T03:00:00Z", "2026-01-15T03:00:00Z")]
    public void Parse_Cycle_ReturnsInclusiveCalendarAndExclusiveUtcBounds(
        string text, string start, string end, string startUtc, string endUtc)
    {
        ExpenseListQuery query = ParseSuccess(text);

        Assert.Equal(new ExpensePeriod(ExpensePeriodKind.BillingCycle, DateOnly.Parse(start), DateOnly.Parse(end)), query.Period);
        Assert.Equal(DateTimeOffset.Parse(startUtc).UtcDateTime, query.PeriodStartUtc);
        Assert.Equal(DateTimeOffset.Parse(endUtc).UtcDateTime, query.PeriodEndExclusiveUtc);
        Assert.Equal(DateTimeKind.Utc, query.PeriodStartUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, query.PeriodEndExclusiveUtc.Kind);
        Assert.Equal(CategoryFilterMode.None, query.CategoryFilter.Mode);
        Assert.Empty(query.CategoryFilter.Categories);
    }

    [Theory]
    [InlineData("10/02/26 - 20/02/2026", "2026-02-10", "2026-02-20", "2026-02-10T03:00:00Z", "2026-02-21T03:00:00Z")]
    [InlineData("28/02/2024-29/02/24", "2024-02-28", "2024-02-29", "2024-02-28T03:00:00Z", "2024-03-01T03:00:00Z")]
    [InlineData("31/12/25 - 01/01/2026", "2025-12-31", "2026-01-01", "2025-12-31T03:00:00Z", "2026-01-02T03:00:00Z")]
    [InlineData("16/02/2019 - 16/02/2019", "2019-02-16", "2019-02-16", "2019-02-16T02:00:00Z", "2019-02-17T03:00:00Z")]
    public void Parse_DateRange_PreservesInclusiveDaysAndHistoricalOffset(
        string text, string start, string end, string startUtc, string endUtc)
    {
        ExpenseListQuery query = ParseSuccess(text);

        Assert.Equal(new ExpensePeriod(ExpensePeriodKind.DateRange, DateOnly.Parse(start), DateOnly.Parse(end)), query.Period);
        Assert.Equal(DateTimeOffset.Parse(startUtc).UtcDateTime, query.PeriodStartUtc);
        Assert.Equal(DateTimeOffset.Parse(endUtc).UtcDateTime, query.PeriodEndExclusiveUtc);
    }

    [Theory]
    [InlineData("Mercado, SAÚDE, mer-ca-do", CategoryFilterMode.Include)]
    [InlineData("04/26 Mercado, SAÚDE, mer-ca-do", CategoryFilterMode.Include)]
    [InlineData("10/02/26 - 20/02/2026 Mercado, SAÚDE, mer-ca-do", CategoryFilterMode.Include)]
    [InlineData("SÉM Mercado, SAÚDE, mer-ca-do", CategoryFilterMode.Exclude)]
    [InlineData("04/26 sem Mercado, SAÚDE, mer-ca-do", CategoryFilterMode.Exclude)]
    [InlineData("10/02/26 - 20/02/2026 sem Mercado, SAÚDE, mer-ca-do", CategoryFilterMode.Exclude)]
    public void Parse_Filter_PreservesModeCategoryOrderAndDuplicates(string text, CategoryFilterMode mode)
    {
        CategoryFilter filter = ParseSuccess(text).CategoryFilter;

        Assert.Equal(mode, filter.Mode);
        Assert.Equal(new[] { Category.Mercado, Category.Saude, Category.Mercado }, filter.Categories);
    }

    [Theory]
    [InlineData("31/02/26 - 20/02/2026 Inexistente", typeof(ListExpensesError.InvalidDate))]
    [InlineData("20/02/26 - 31/02/2026 sem", typeof(ListExpensesError.InvalidDate))]
    [InlineData("10/02/26", typeof(ListExpensesError.InvalidDate))]
    [InlineData("10/02/2026 até 20/02/2026", typeof(ListExpensesError.InvalidDate))]
    [InlineData("20/02/26 - 10/02/2026 Inexistente", typeof(ListExpensesError.InvertedPeriod))]
    [InlineData("20/02/26 - 10/02/2026 sem", typeof(ListExpensesError.InvertedPeriod))]
    [InlineData("13/26 Inexistente", typeof(ListExpensesError.InvalidMonth))]
    [InlineData("00/26 sem", typeof(ListExpensesError.InvalidMonth))]
    [InlineData("sem", typeof(ListExpensesError.MissingExcludedCategories))]
    [InlineData("04/26 sem", typeof(ListExpensesError.MissingExcludedCategories))]
    [InlineData("10/02/26 - 20/02/2026 sem", typeof(ListExpensesError.MissingExcludedCategories))]
    public void Parse_InvalidInput_ReturnsSpecificFailureInExistingPrecedence(string text, Type errorType)
    {
        var failure = Assert.IsType<ExpenseListQueryParseResult.Failure>(ExpenseListQueryParser.Parse(text, MessageDate));

        Assert.IsType(errorType, failure.Error);
    }

    [Theory]
    [InlineData("Mercado, primeira, segunda", "primeira")]
    [InlineData("sem Mercado, primeira, segunda", "primeira")]
    [InlineData("Mercado,", "")]
    [InlineData("04/2026", "04/2026")]
    [InlineData("04/26\tMercado", "04/26\tMercado")]
    [InlineData("sem\tMercado", "sem\tMercado")]
    public void Parse_UnknownCategory_PreservesFirstInvalidTextAndTokenSyntax(string text, string unknown)
    {
        var failure = Assert.IsType<ExpenseListQueryParseResult.Failure>(ExpenseListQueryParser.Parse(text, MessageDate));

        Assert.Equal(new ListExpensesError.UnknownCategory(unknown), failure.Error);
    }

    private static ExpenseListQuery ParseSuccess(string text) =>
        Assert.IsType<ExpenseListQueryParseResult.Success>(ExpenseListQueryParser.Parse(text, MessageDate)).Query;
}
