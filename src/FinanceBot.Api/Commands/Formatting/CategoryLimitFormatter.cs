using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Formatting;

public static class CategoryLimitFormatter
{
    public static string Format(CategoryLimitResult result) => result switch
    {
        CategoryLimitResult.Queried { Progress: null } query =>
            $"Nenhum limite definido para {CategoryCatalog.GetDisplayName(query.Category)}.",
        CategoryLimitResult.Queried { Progress: { } progress } =>
            $"{CategoryCatalog.GetDisplayName(progress.Categoria)}: R$ {BrazilianCurrencyFormatter.FormatAmount(progress.TotalGasto)} de R$ {BrazilianCurrencyFormatter.FormatAmount(progress.Limite)} ({progress.Percentual}%)",
        CategoryLimitResult.Updated updated =>
            $"Limite de {CategoryCatalog.GetDisplayName(updated.Category)} definido: R$ {BrazilianCurrencyFormatter.FormatAmount(updated.Value)}",
        CategoryLimitResult.Removed removed =>
            $"Limite de {CategoryCatalog.GetDisplayName(removed.Category)} removido",
        CategoryLimitResult.Failure { Error: CategoryLimitError.InvalidSyntax } =>
            "Uso correto: /limite <CATEGORIA> <VALOR>. Exemplo: /limite Mercado 800\n" +
            "Para consultar: /limite <CATEGORIA>. Exemplo: /limite Mercado",
        CategoryLimitResult.Failure { Error: CategoryLimitError.UnknownCategory error } =>
            CategoryCommandMessages.InvalidCategory(error.Text),
        CategoryLimitResult.Failure { Error: CategoryLimitError.InvalidLimit } =>
            "Valor invalido. Use um numero positivo ou 0 para remover o limite.",
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };
}
