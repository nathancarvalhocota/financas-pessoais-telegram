using System.Globalization;
using System.Text;

namespace FinanceBot.Domain.Categories;

public static class CategoryCatalog
{
    private static readonly Dictionary<string, Categoria> CategoriesByNormalizedText =
        new Dictionary<string, Categoria>(StringComparer.Ordinal)
        {
            ["educacao"] = Categoria.Educacao,
            ["lazer"] = Categoria.Lazer,
            ["lanches"] = Categoria.Lanches,
            ["uber"] = Categoria.Uber,
            ["mercado"] = Categoria.Mercado,
            ["moto"] = Categoria.Moto,
            ["compras"] = Categoria.Compras,
            ["outros"] = Categoria.Outros,
            ["estetica"] = Categoria.Estetica,
            ["limpeza"] = Categoria.Limpeza,
            ["saude"] = Categoria.Saude
        };

    public static IReadOnlyList<Categoria> GetAll()
    {
        return Enum.GetValues<Categoria>();
    }

    public static bool TryParse(string text, out Categoria category)
    {
        return CategoriesByNormalizedText.TryGetValue(Normalize(text), out category);
    }

    public static string GetDisplayName(Categoria category)
    {
        return category switch
        {
            Categoria.Educacao => "Educacao",
            Categoria.Lazer => "Lazer",
            Categoria.Lanches => "Lanches",
            Categoria.Uber => "Uber",
            Categoria.Mercado => "Mercado",
            Categoria.Moto => "Moto",
            Categoria.Compras => "Compras",
            Categoria.Outros => "Outros",
            Categoria.Estetica => "Estetica",
            Categoria.Limpeza => "Limpeza",
            Categoria.Saude => "Saude",
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
