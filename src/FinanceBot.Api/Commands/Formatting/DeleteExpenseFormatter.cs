using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Formatting;

public static class DeleteExpenseFormatter
{
    public static string Format(DeleteExpenseResult result) => result switch
    {
        DeleteExpenseResult.Success success =>
            $"Compra removida: R$ {BrazilianCurrencyFormatter.FormatAmount(success.Expense.Amount)} - {success.Expense.Description} ({CategoryCatalog.GetDisplayName(success.Expense.Category)})",
        DeleteExpenseResult.NotFound =>
            "ID nao encontrado. Nada foi removido.",
        DeleteExpenseResult.Failure { Error: DeleteExpenseError.InvalidSyntax } =>
            "Uso correto: /deletar <ID>. Exemplo: /deletar 42",
        DeleteExpenseResult.Failure { Error: DeleteExpenseError.InvalidId } =>
            "ID invalido para o comando /deletar.",
        _ => throw new ArgumentOutOfRangeException(nameof(result))
    };
}
