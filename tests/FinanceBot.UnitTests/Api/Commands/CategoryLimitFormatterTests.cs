using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class CategoryLimitFormatterTests
{
    [Fact]
    public void Format_QueryWithoutLimit_PreservesMessage()
    {
        Assert.Equal("Nenhum limite definido para Mercado.",
            CategoryLimitFormatter.Format(new CategoryLimitResult.Queried(Category.Mercado, null)));
    }

    [Theory]
    [InlineData(340, 42, false, "Mercado: R$ 340,00 de R$ 800,00 (42%)")]
    [InlineData(800, 100, false, "Mercado: R$ 800,00 de R$ 800,00 (100%)")]
    [InlineData(1000, 125, true, "Mercado: R$ 1.000,00 de R$ 800,00 (125%)")]
    public void Format_QueryWithProgress_PreservesMessage(double spending, int percentage, bool exceeded, string expected)
    {
        var progress = new CategoryLimitProgress(Category.Mercado, 800, spending, percentage, exceeded);

        Assert.Equal(expected, CategoryLimitFormatter.Format(new CategoryLimitResult.Queried(Category.Mercado, progress)));
    }

    [Fact]
    public void Format_UpdateAndRemoval_PreserveMessages()
    {
        Assert.Equal("Limite de Mercado definido: R$ 1.200,25",
            CategoryLimitFormatter.Format(new CategoryLimitResult.Updated(Category.Mercado, 1200.25)));
        Assert.Equal("Limite de Mercado removido",
            CategoryLimitFormatter.Format(new CategoryLimitResult.Removed(Category.Mercado)));
    }

    [Fact]
    public void Format_SyntaxAndValueErrors_PreserveMessages()
    {
        Assert.Equal("Uso correto: /limite <CATEGORIA> <VALOR>. Exemplo: /limite Mercado 800\nPara consultar: /limite <CATEGORIA>. Exemplo: /limite Mercado",
            CategoryLimitFormatter.Format(new CategoryLimitResult.Failure(new CategoryLimitError.InvalidSyntax())));
        Assert.Equal("Valor invalido. Use um numero positivo ou 0 para remover o limite.",
            CategoryLimitFormatter.Format(new CategoryLimitResult.Failure(new CategoryLimitError.InvalidLimit())));
    }

    [Fact]
    public void Format_UnknownCategory_PreservesTextAndCategoryOrder()
    {
        Assert.Equal("Categoria 'Desconhecida' inexistente.\n\nCategorias disponiveis:\nEducacao\nLazer\nLanches\nUber\nMercado\nMoto\nCompras\nOutros\nEstetica\nLimpeza\nSaude",
            CategoryLimitFormatter.Format(new CategoryLimitResult.Failure(new CategoryLimitError.UnknownCategory("Desconhecida"))));
    }
}
