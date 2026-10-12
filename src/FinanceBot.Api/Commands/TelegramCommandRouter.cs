using FinanceBot.Api.Commands.Handlers;
using FinanceBot.Api.Commands.Parsing;
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

    public Task<string> RouteMessageAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        TelegramCommand command = TelegramCommandParser.Parse(messageText);

        return command.Name switch
        {
            "/compra" => _registerExpenseCommandHandler.HandleRegisterExpenseAsync(
                command.Text,
                messageDateUtc,
                cancellationToken),
            "/listar" => _listExpensesCommandHandler.HandleListExpensesAsync(
                command.Text,
                messageDateUtc,
                cancellationToken),
            "/deletar" => _deleteExpenseCommandHandler.HandleDeleteExpenseAsync(
                command.Text,
                cancellationToken),
            "/limite" => _categoryLimitCommandHandler.HandleSetCategoryLimitAsync(
                command.Text,
                cancellationToken),
            "/start" => Task.FromResult(TelegramCommandMessages.Start),
            "/info" => Task.FromResult(TelegramCommandMessages.Info),
            _ => Task.FromResult(TelegramCommandMessages.UnknownCommand)
        };
    }
}
