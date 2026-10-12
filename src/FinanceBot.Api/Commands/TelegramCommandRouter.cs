using FinanceBot.Api.Commands.Handlers;
using FinanceBot.Api.Commands.Parsing;
using FinanceBot.Api.Commands.Formatting;
using FinanceBot.Domain.Expenses;
using FinanceBot.Domain.CategoryLimits;

namespace FinanceBot.Api.Commands;

public sealed class TelegramCommandRouter
{
    private readonly RegisterExpenseCommandHandler _registerExpenseCommandHandler;
    private readonly ListExpensesCommandHandler _listExpensesCommandHandler;
    private readonly DeleteExpenseCommandHandler _deleteExpenseCommandHandler;
    private readonly CategoryLimitCommandHandler _categoryLimitCommandHandler;

    public TelegramCommandRouter(
        RegisterExpenseCommandHandler registerExpenseCommandHandler,
        ListExpensesCommandHandler listExpensesCommandHandler,
        DeleteExpenseCommandHandler deleteExpenseCommandHandler,
        CategoryLimitCommandHandler categoryLimitCommandHandler)
    {
        _registerExpenseCommandHandler = registerExpenseCommandHandler;
        _listExpensesCommandHandler = listExpensesCommandHandler;
        _deleteExpenseCommandHandler = deleteExpenseCommandHandler;
        _categoryLimitCommandHandler = categoryLimitCommandHandler;
    }

    public TelegramCommandRouter(
        ICompraRepository expenseRepository,
        ILimiteCategoriaRepository categoryLimitRepository)
        : this(
            new RegisterExpenseCommandHandler(expenseRepository, categoryLimitRepository),
            new ListExpensesCommandHandler(expenseRepository),
            new DeleteExpenseCommandHandler(expenseRepository),
            new CategoryLimitCommandHandler(expenseRepository, categoryLimitRepository, TimeProvider.System))
    {
    }

    public async Task<string> RouteMessageAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        TelegramCommand command = TelegramCommandParser.Parse(messageText);

        return command.Name switch
        {
            "/compra" => RegisterExpenseFormatter.Format(await _registerExpenseCommandHandler.HandleRegisterExpenseAsync(
                command.Text,
                messageDateUtc,
                cancellationToken)),
            "/listar" => await _listExpensesCommandHandler.HandleListExpensesAsync(
                command.Text,
                messageDateUtc,
                cancellationToken),
            "/deletar" => await _deleteExpenseCommandHandler.HandleDeleteExpenseAsync(
                command.Text,
                cancellationToken),
            "/limite" => CategoryLimitFormatter.Format(await _categoryLimitCommandHandler.HandleSetCategoryLimitAsync(
                command.Text,
                cancellationToken)),
            "/start" => TelegramCommandMessages.Start,
            "/info" => TelegramCommandMessages.Info,
            _ => TelegramCommandMessages.UnknownCommand
        };
    }
}
