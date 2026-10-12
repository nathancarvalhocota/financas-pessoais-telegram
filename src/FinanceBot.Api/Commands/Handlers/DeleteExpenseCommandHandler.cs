using System.Text.RegularExpressions;
using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Domain.Expenses;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Handlers;

public sealed class DeleteExpenseCommandHandler
{
    private static readonly Regex DeleteExpenseCommandRegex = new Regex(
        "^/deletar\\s+(\\d+)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly ICompraRepository _expenseRepository;

    public DeleteExpenseCommandHandler(ICompraRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async Task<string> HandleDeleteExpenseAsync(string messageText, CancellationToken cancellationToken)
    {
        Match commandMatch = DeleteExpenseCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return "Uso correto: /deletar <ID>. Exemplo: /deletar 42";
        }

        bool parsedId = int.TryParse(commandMatch.Groups[1].Value, out int expenseId);
        if (!parsedId || expenseId <= 0)
        {
            return "ID invalido para o comando /deletar.";
        }

        Compra? expense = await _expenseRepository.FindByIdAsync(expenseId, cancellationToken);
        if (expense is null)
        {
            return "ID nao encontrado. Nada foi removido.";
        }

        await _expenseRepository.DeleteAsync(expense, cancellationToken);

        string categoryDisplayName = CategoryCatalog.GetDisplayName(expense.Categoria);
        return $"Compra removida: R$ {BrazilianCurrencyFormatter.FormatAmount(expense.Valor)} - {expense.Descricao} ({categoryDisplayName})";
    }
}
