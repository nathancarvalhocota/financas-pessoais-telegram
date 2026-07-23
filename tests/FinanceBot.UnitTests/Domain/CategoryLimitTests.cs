using FinanceBot.Domain.CategoryLimits;
using Xunit;

namespace FinanceBot.UnitTests.Domain;

public sealed class CategoryLimitTests
{
    [Fact]
    public void IsExceededBy_ReturnsTrueOnlyAboveLimit()
    {
        LimiteCategoria limit = new LimiteCategoria { Valor = 800 };

        Assert.False(limit.IsExceededBy(800));
        Assert.True(limit.IsExceededBy(800.01));
    }

    [Fact]
    public void CalculateUsagePercentage_RoundsUsingCurrentRule()
    {
        LimiteCategoria limit = new LimiteCategoria { Valor = 800 };

        Assert.Equal(42, limit.CalculateUsagePercentage(340));
    }
}
