using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;
using Xunit;

namespace FinanceBot.UnitTests.Domain;

public sealed class ExpenseTests
{
    [Fact]
    public void Create_WithValidData_PreservesFieldsWithoutNewDescriptionLengthRule()
    {
        DateTime date = new(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        string description = new('x', 300);

        Expense expense = Assert.IsType<ExpenseCreationResult.Success>(Expense.Create(58.90, description, Category.Mercado, date)).Expense;

        Assert.Equal(58.90, expense.Amount);
        Assert.Equal(description, expense.Description);
        Assert.Equal(Category.Mercado, expense.Category);
        Assert.Equal(date, expense.DateUtc);
        Assert.Equal(0, expense.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Create_WithInvalidAmount_ReturnsDomainError(double amount)
    {
        var result = Assert.IsType<ExpenseCreationResult.Failure>(Expense.Create(amount, "Compra", Category.Mercado, DateTime.UtcNow));

        Assert.Equal(ExpenseError.InvalidAmount, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Create_WithMissingDescription_ReturnsDomainError(string? description)
    {
        var result = Assert.IsType<ExpenseCreationResult.Failure>(Expense.Create(1, description!, Category.Mercado, DateTime.UtcNow));

        Assert.Equal(ExpenseError.MissingDescription, result.Error);
    }

    [Fact]
    public void Create_WithUndefinedCategory_ReturnsDomainError()
    {
        var result = Assert.IsType<ExpenseCreationResult.Failure>(Expense.Create(1, "Compra", (Category)999, DateTime.UtcNow));

        Assert.Equal(ExpenseError.InvalidCategory, result.Error);
    }

    [Fact]
    public void PublicSurface_DoesNotAllowBypassingValidation()
    {
        Assert.Empty(typeof(Expense).GetConstructors());
        Assert.All(typeof(Expense).GetProperties(), property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }
}
