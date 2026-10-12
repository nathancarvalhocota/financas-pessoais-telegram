using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using Xunit;

namespace FinanceBot.UnitTests.Domain;

public sealed class CategoryLimitTests
{
    [Fact]
    public void Create_WithValidData_PreservesCategoryAndValue()
    {
        CategoryLimit limit = CreateLimit();

        Assert.Equal(Category.Mercado, limit.Category);
        Assert.Equal(800, limit.Amount);
        Assert.Equal(0, limit.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Create_WithInvalidValue_ReturnsDomainError(double value)
    {
        var failure = Assert.IsType<CategoryLimitCreationResult.Failure>(CategoryLimit.Create(Category.Mercado, value));

        Assert.Equal(CategoryLimitValidationError.InvalidValue, failure.Error);
    }

    [Fact]
    public void Create_WithUndefinedCategory_ReturnsDomainError()
    {
        var failure = Assert.IsType<CategoryLimitCreationResult.Failure>(CategoryLimit.Create((Category)999, 100));

        Assert.Equal(CategoryLimitValidationError.InvalidCategory, failure.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void UpdateAmount_WithInvalidValue_PreservesState(double value)
    {
        CategoryLimit limit = CreateLimit();

        Assert.Equal(CategoryLimitValidationError.InvalidValue, limit.UpdateAmount(value));
        Assert.Equal(800, limit.Amount);
        Assert.Equal(Category.Mercado, limit.Category);
        Assert.Equal(0, limit.Id);
    }

    [Fact]
    public void UpdateAmount_WithPositiveFiniteValue_UpdatesLimit()
    {
        CategoryLimit limit = CreateLimit();

        Assert.Null(limit.UpdateAmount(900.25));
        Assert.Equal(900.25, limit.Amount);
        Assert.Equal(Category.Mercado, limit.Category);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(340, 42, false)]
    [InlineData(348, 44, false)]
    [InlineData(800, 100, false)]
    [InlineData(800.01, 100, true)]
    [InlineData(1200, 150, true)]
    public void EvaluateProgress_ReturnsDataWithCurrentRoundingAndStrictExceedingRule(
        double spending, int percentage, bool exceeded)
    {
        CategoryLimit limit = CreateLimit();

        CategoryLimitProgress progress = limit.EvaluateProgress(spending);

        Assert.Equal(new CategoryLimitProgress(Category.Mercado, 800, spending, percentage, exceeded), progress);
        Assert.Equal(800, limit.Amount);
    }

    [Fact]
    public void PublicSurface_DoesNotAllowBypassingValidation()
    {
        Assert.Empty(typeof(CategoryLimit).GetConstructors());
        Assert.All(typeof(CategoryLimit).GetProperties(), property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    private static CategoryLimit CreateLimit() =>
        Assert.IsType<CategoryLimitCreationResult.Success>(CategoryLimit.Create(Category.Mercado, 800)).Limit;
}
