using System.Text.RegularExpressions;
using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Api.Commands.Parsing;
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

    public async Task<string> HandleListExpensesAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        Match commandMatch = ListExpensesCommandRegex.Match(messageText);
        if (!commandMatch.Success)
        {
            return "Uso correto: /listar <MM/YY>. Exemplo: /listar 09/26";
        }

        ExpenseListQueryParseResult parseResult = ExpenseListQueryParser.Parse(
            commandMatch.Groups[1].Value.Trim(),
            messageDateUtc);

        if (!parseResult.IsSuccess)
        {
            return parseResult.ErrorMessage!;
        }

        ExpenseListQuery query = parseResult.Query!;
        IReadOnlyList<Compra> expenses = await _expenseRepository.ListByPeriodAsync(
            query.PeriodStartUtc,
            query.PeriodEndUtc.AddDays(1),
            cancellationToken);

        return ExpenseListFormatter.Format(query, expenses);
    }
}
