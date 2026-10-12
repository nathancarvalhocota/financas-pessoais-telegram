using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;
using Xunit;

namespace FinanceBot.UnitTests.Domain;

public sealed class CompraTests
{
    [Fact]
    public void Criar_WithValidData_PreservesFieldsWithoutNewDescriptionLengthRule()
    {
        DateTime date = new(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        string description = new('x', 300);

        Compra expense = Assert.IsType<CompraCreationResult.Success>(Compra.Criar(58.90, description, Categoria.Mercado, date)).Compra;

        Assert.Equal(58.90, expense.Valor);
        Assert.Equal(description, expense.Descricao);
        Assert.Equal(Categoria.Mercado, expense.Categoria);
        Assert.Equal(date, expense.Data);
        Assert.Equal(0, expense.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Criar_WithInvalidAmount_ReturnsDomainError(double amount)
    {
        var result = Assert.IsType<CompraCreationResult.Failure>(Compra.Criar(amount, "Compra", Categoria.Mercado, DateTime.UtcNow));

        Assert.Equal(CompraError.InvalidAmount, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void Criar_WithMissingDescription_ReturnsDomainError(string? description)
    {
        var result = Assert.IsType<CompraCreationResult.Failure>(Compra.Criar(1, description!, Categoria.Mercado, DateTime.UtcNow));

        Assert.Equal(CompraError.MissingDescription, result.Error);
    }

    [Fact]
    public void Criar_WithUndefinedCategory_ReturnsDomainError()
    {
        var result = Assert.IsType<CompraCreationResult.Failure>(Compra.Criar(1, "Compra", (Categoria)999, DateTime.UtcNow));

        Assert.Equal(CompraError.InvalidCategory, result.Error);
    }

    [Fact]
    public void PublicSurface_DoesNotAllowBypassingValidation()
    {
        Assert.Empty(typeof(Compra).GetConstructors());
        Assert.All(typeof(Compra).GetProperties(), property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }
}
