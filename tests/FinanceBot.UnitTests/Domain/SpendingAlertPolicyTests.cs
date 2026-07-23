using FinanceBot.Domain.Billing;
using Xunit;

namespace FinanceBot.UnitTests.Domain;

public sealed class SpendingAlertPolicyTests
{
    [Theory]
    [InlineData(100, 124, 125, SpendingAlertLevel.Warning)]
    [InlineData(100, 149, 150, SpendingAlertLevel.Danger)]
    [InlineData(100, 199, 200, SpendingAlertLevel.Urgent)]
    public void GetCrossedLevel_ReturnsHighestCrossedLevel(
        double previousTotal,
        double totalBeforeExpense,
        double currentTotal,
        SpendingAlertLevel expectedLevel)
    {
        SpendingAlertLevel? level = SpendingAlertPolicy.GetCrossedLevel(
            previousTotal,
            totalBeforeExpense,
            currentTotal);

        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void GetCrossedLevel_WithoutPreviousSpending_ReturnsNull()
    {
        SpendingAlertLevel? level = SpendingAlertPolicy.GetCrossedLevel(0, 0, 200);

        Assert.Null(level);
    }
}
