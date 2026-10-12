using System.Globalization;
using System.Text;

namespace FinanceBot.Domain.Categories;

public static class CategoryCatalog
{
    private static readonly Dictionary<string, Category> CategoriesByNormalizedText =
        new Dictionary<string, Category>(StringComparer.Ordinal)
        {
            ["educacao"] = Category.Educacao,
            ["lazer"] = Category.Lazer,
            ["lanches"] = Category.Lanches,
            ["uber"] = Category.Uber,
            ["mercado"] = Category.Mercado,
            ["moto"] = Category.Moto,
            ["compras"] = Category.Compras,
            ["outros"] = Category.Outros,
            ["estetica"] = Category.Estetica,
            ["limpeza"] = Category.Limpeza,
            ["saude"] = Category.Saude
        };

    public static IReadOnlyList<Category> GetAll()
    {
        return Enum.GetValues<Category>();
    }

    public static bool TryParse(string text, out Category category)
    {
        return CategoriesByNormalizedText.TryGetValue(Normalize(text), out category);
    }

    public static string GetDisplayName(Category category)
    {
        return category switch
        {
            Category.Educacao => "Educacao",
            Category.Lazer => "Lazer",
            Category.Lanches => "Lanches",
            Category.Uber => "Uber",
            Category.Mercado => "Mercado",
            Category.Moto => "Moto",
            Category.Compras => "Compras",
            Category.Outros => "Outros",
            Category.Estetica => "Estetica",
            Category.Limpeza => "Limpeza",
            Category.Saude => "Saude",
            _ => category.ToString()
        };
    }

    private static string Normalize(string text)
    {
        string normalizedUnicodeText = text.Normalize(NormalizationForm.FormD);
        StringBuilder normalizedBuilder = new StringBuilder();

        foreach (char character in normalizedUnicodeText)
        {
            UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                normalizedBuilder.Append(char.ToLowerInvariant(character));
            }
        }

        return normalizedBuilder.ToString();
    }
}
