using FinanceBot.Domain.Categories;
using Xunit;

namespace FinanceBot.UnitTests.Domain;

public sealed class CategoryCatalogTests
{
    [Theory]
    [InlineData("Mercado")]
    [InlineData("MERCADO")]
    [InlineData("  mercado  ")]
    [InlineData("Mercádo")]
    public void TryParse_WithEquivalentText_ReturnsCategory(string text)
    {
        bool parsed = CategoryCatalog.TryParse(text, out Categoria category);

        Assert.True(parsed);
        Assert.Equal(Categoria.Mercado, category);
    }

    [Fact]
    public void TryParse_WithUnknownText_ReturnsFalse()
    {
        bool parsed = CategoryCatalog.TryParse("Unknown", out _);

        Assert.False(parsed);
    }

    [Fact]
    public void GetDisplayName_ReturnsBusinessLabel()
    {
        Assert.Equal("Saude", CategoryCatalog.GetDisplayName(Categoria.Saude));
    }
}
