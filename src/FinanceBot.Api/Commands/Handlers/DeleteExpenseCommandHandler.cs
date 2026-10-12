using System.Text.RegularExpressions;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Expenses;

namespace FinanceBot.Api.Commands.Handlers;

public sealed class DeleteExpenseCommandHandler
{
    private static readonly Regex DeleteExpenseCommandRegex = new Regex(
        "^/deletar\\s+(\\d+)\\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly IExpenseRepository _expenseRepository;

    public DeleteExpenseCommandHandler(IExpenseRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async Task<DeleteExpenseResult> HandleDeleteExpenseAsync(string messageText, CancellationToken cancellationToken)
    {
        Match commandMatch = DeleteExpenseCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return new DeleteExpenseResult.Failure(new DeleteExpenseError.InvalidSyntax());
        }

        bool parsedId = int.TryParse(commandMatch.Groups[1].Value, out int expenseId);
        if (!parsedId || expenseId <= 0)
        {
            return new DeleteExpenseResult.Failure(new DeleteExpenseError.InvalidId());
        }

        Expense? expense = await _expenseRepository.FindByIdAsync(expenseId, cancellationToken);
        if (expense is null)
        {
            return new DeleteExpenseResult.NotFound(expenseId);
        }

        await _expenseRepository.DeleteAsync(expense, cancellationToken);

        return new DeleteExpenseResult.Success(ExpenseItem.From(expense));
    }
}
