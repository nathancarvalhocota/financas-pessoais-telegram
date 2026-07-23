using FinanceBot.Domain.Categories;

namespace FinanceBot.Application.Commands.Formatting;

internal static class CategoryCommandMessages
{
    public static string InvalidCategory(string categoryText)
    {
        return $"Categoria '{categoryText}' inexistente.\n\nCategorias disponiveis:\n" +
            string.Join("\n", CategoryCatalog.GetAll().Select(CategoryCatalog.GetDisplayName));
    }
}
