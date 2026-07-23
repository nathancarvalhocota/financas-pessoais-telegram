using FinanceBot.Domain.Billing;
using Xunit;

namespace FinanceBot.UnitTests.Domain;

public sealed class BillingCycleTests
{
    [Theory]
    [InlineData(2026, 7, 14, 2026, 6, 15, 2026, 7, 14)]
    [InlineData(2026, 7, 15, 2026, 7, 15, 2026, 8, 14)]
    [InlineData(2026, 1, 1, 2025, 12, 15, 2026, 1, 14)]
    public void From_ReturnsExpectedCycle(
        int year,
        int month,
        int day,
        int expectedStartYear,
        int expectedStartMonth,
        int expectedStartDay,
        int expectedEndYear,
        int expectedEndMonth,
        int expectedEndDay)
    {
        BillingCycle cycle = BillingCycle.GetBillingCycle(new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(expectedStartYear, expectedStartMonth, expectedStartDay), cycle.StartDate);
        Assert.Equal(new DateOnly(expectedEndYear, expectedEndMonth, expectedEndDay), cycle.EndDate);
    }
}
