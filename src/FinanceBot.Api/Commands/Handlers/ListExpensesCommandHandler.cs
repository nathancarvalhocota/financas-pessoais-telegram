using System.Text.RegularExpressions;
using FinanceBot.Api.Commands.Parsing;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Expenses;

namespace FinanceBot.Api.Commands.Handlers;

public sealed class ListExpensesCommandHandler
{
    private static readonly Regex ListExpensesCommandRegex = new Regex(
        "^/listar(?:\\s+(.+))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly ICompraRepository _expenseRepository;

    public ListExpensesCommandHandler(ICompraRepository expenseRepository)
    {
        _expenseRepository = expenseRepository;
    }

    public async Task<ListExpensesResult> HandleListExpensesAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        Match commandMatch = ListExpensesCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return new ListExpensesResult.Failure(new ListExpensesError.InvalidSyntax());
        }

        ExpenseListQueryParseResult parseResult = ExpenseListQueryParser.Parse(
            commandMatch.Groups[1].Value.Trim(),
            messageDateUtc);

        if (parseResult is ExpenseListQueryParseResult.Failure failure)
        {
            return new ListExpensesResult.Failure(failure.Error);
        }

        ExpenseListQuery query = ((ExpenseListQueryParseResult.Success)parseResult).Query;
        IReadOnlyList<Compra> expenses = await _expenseRepository.ListByPeriodAsync(
            query.PeriodStartUtc,
            query.PeriodEndExclusiveUtc,
            cancellationToken);

        List<ExpenseItem> items = new();
        double total = 0;
        foreach (Compra expense in expenses)
        {
            bool included = query.CategoryFilter.Mode switch
            {
                CategoryFilterMode.None => true,
                CategoryFilterMode.Include => query.CategoryFilter.Categories.Contains(expense.Categoria),
                CategoryFilterMode.Exclude => !query.CategoryFilter.Categories.Contains(expense.Categoria),
                _ => false
            };
            if (!included) continue;

            items.Add(ExpenseItem.From(expense));
            total += expense.Valor;
        }

        return new ListExpensesResult.Success(query.Period, query.CategoryFilter, items, total);
    }
}
