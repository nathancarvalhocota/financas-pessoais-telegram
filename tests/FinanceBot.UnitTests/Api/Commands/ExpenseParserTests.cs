using FinanceBot.Api.Commands.Parsing;
using FinanceBot.Domain.Categories;
using Xunit;

namespace FinanceBot.UnitTests.Api.Commands;

public sealed class ExpenseParserTests
{
    [Theory]
    [InlineData("/compra 58,90,  Almoco  , mÉr-ca do", 58.90, "Almoco", Category.Mercado)]
    [InlineData("/COMPRA@FinanceBot 58.9, Almoco, SAÚDE", 58.9, "Almoco", Category.Saude)]
    [InlineData("/compra 58, Almoco, Mercado", 58, "Almoco", Category.Mercado)]
    public void Parse_ValidSyntax_ReturnsData(string text, double amount, string description, Category category)
    {
        var parsed = Assert.IsType<ExpenseParseResult.Success>(ExpenseParser.Parse(text));

        Assert.Equal(amount, parsed.Amount);
        Assert.Equal(description, parsed.Description);
        Assert.Equal(category, parsed.Category);
    }

    [Fact]
    public void Parse_ExtraComma_PreservesExistingCategoryInterpretation()
    {
        var failure = Assert.IsType<ExpenseParseResult.Failure>(ExpenseParser.Parse("/compra 1, Descricao, com virgula, Mercado"));

        Assert.Equal(new ExpenseParseError.UnknownCategory("com virgula, Mercado"), failure.Error);
    }

    [Theory]
    [InlineData("/compra")]
    [InlineData("/compra -1, Teste, Inexistente")]
    [InlineData("/compra 1.001, Teste, Mercado")]
    [InlineData("/compra NaN, Teste, Mercado")]
    [InlineData("/compra Infinity, Teste, Mercado")]
    [InlineData("/compra 1, ,")]
    public void Parse_InvalidSyntax_PrecedesOtherErrors(string text)
    {
        var failure = Assert.IsType<ExpenseParseResult.Failure>(ExpenseParser.Parse(text));

        Assert.IsType<ExpenseParseError.InvalidSyntax>(failure.Error);
    }

    [Theory]
    [InlineData("/compra 0, , Inexistente", typeof(ExpenseParseError.InvalidAmount))]
    [InlineData("/compra 1,   , Inexistente", typeof(ExpenseParseError.MissingDescription))]
    [InlineData("/compra 1, Teste, Inexistente", typeof(ExpenseParseError.UnknownCategory))]
    public void Parse_FieldErrors_PreserveValueDescriptionCategoryPrecedence(string text, Type errorType)
    {
        var failure = Assert.IsType<ExpenseParseResult.Failure>(ExpenseParser.Parse(text));

        Assert.IsType(errorType, failure.Error);
    }

    [Fact]
    public void Parse_OverflowAmount_ReturnsAmountErrorBeforeUnknownCategory()
    {
        var failure = Assert.IsType<ExpenseParseResult.Failure>(ExpenseParser.Parse("/compra " + new string('9', 400) + ", Teste, Inexistente"));

        Assert.IsType<ExpenseParseError.InvalidAmount>(failure.Error);
    }
}
