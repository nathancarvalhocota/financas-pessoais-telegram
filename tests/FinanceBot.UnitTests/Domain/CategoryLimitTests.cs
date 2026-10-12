using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using Xunit;

namespace FinanceBot.UnitTests.Domain;

public sealed class CategoryLimitTests
{
    [Fact]
    public void Criar_WithValidData_PreservesCategoryAndValue()
    {
        LimiteCategoria limit = CreateLimit();

        Assert.Equal(Categoria.Mercado, limit.Categoria);
        Assert.Equal(800, limit.Valor);
        Assert.Equal(0, limit.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Criar_WithInvalidValue_ReturnsDomainError(double value)
    {
        var failure = Assert.IsType<LimiteCategoriaCreationResult.Failure>(LimiteCategoria.Criar(Categoria.Mercado, value));

        Assert.Equal(LimiteCategoriaError.InvalidValue, failure.Error);
    }

    [Fact]
    public void Criar_WithUndefinedCategory_ReturnsDomainError()
    {
        var failure = Assert.IsType<LimiteCategoriaCreationResult.Failure>(LimiteCategoria.Criar((Categoria)999, 100));

        Assert.Equal(LimiteCategoriaError.InvalidCategory, failure.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AlterarValor_WithInvalidValue_PreservesState(double value)
    {
        LimiteCategoria limit = CreateLimit();

        Assert.Equal(LimiteCategoriaError.InvalidValue, limit.AlterarValor(value));
        Assert.Equal(800, limit.Valor);
        Assert.Equal(Categoria.Mercado, limit.Categoria);
        Assert.Equal(0, limit.Id);
    }

    [Fact]
    public void AlterarValor_WithPositiveFiniteValue_UpdatesLimit()
    {
        LimiteCategoria limit = CreateLimit();

        Assert.Null(limit.AlterarValor(900.25));
        Assert.Equal(900.25, limit.Valor);
        Assert.Equal(Categoria.Mercado, limit.Categoria);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(340, 42, false)]
    [InlineData(348, 44, false)]
    [InlineData(800, 100, false)]
    [InlineData(800.01, 100, true)]
    [InlineData(1200, 150, true)]
    public void AvaliarProgresso_ReturnsDataWithCurrentRoundingAndStrictExceedingRule(
        double spending, int percentage, bool exceeded)
    {
        LimiteCategoria limit = CreateLimit();

        CategoryLimitProgress progress = limit.AvaliarProgresso(spending);

        Assert.Equal(new CategoryLimitProgress(Categoria.Mercado, 800, spending, percentage, exceeded), progress);
        Assert.Equal(800, limit.Valor);
    }

    [Fact]
    public void PublicSurface_DoesNotAllowBypassingValidation()
    {
        Assert.Empty(typeof(LimiteCategoria).GetConstructors());
        Assert.All(typeof(LimiteCategoria).GetProperties(), property => Assert.False(property.SetMethod?.IsPublic ?? false));
    }

    private static LimiteCategoria CreateLimit() =>
        Assert.IsType<LimiteCategoriaCreationResult.Success>(LimiteCategoria.Criar(Categoria.Mercado, 800)).Limite;
}
