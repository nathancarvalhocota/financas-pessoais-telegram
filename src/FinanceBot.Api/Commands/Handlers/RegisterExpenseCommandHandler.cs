using FinanceBot.Api.Commands.Parsing;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Expenses;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Billing;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Handlers;

public sealed class RegisterExpenseCommandHandler
{
    private readonly ICompraRepository _expenseRepository;
    private readonly ILimiteCategoriaRepository _categoryLimitRepository;

    public RegisterExpenseCommandHandler(
        ICompraRepository expenseRepository,
        ILimiteCategoriaRepository categoryLimitRepository)
    {
        _expenseRepository = expenseRepository;
        _categoryLimitRepository = categoryLimitRepository;
    }

    public async Task<RegisterExpenseResult> HandleRegisterExpenseAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken)
    {
        ExpenseParseResult parsed = ExpenseParser.Parse(messageText);
        if (parsed is ExpenseParseResult.Failure failure)
        {
            return new RegisterExpenseResult.Failure(failure.Error switch
            {
                ExpenseParseError.InvalidSyntax => new RegisterExpenseError.InvalidSyntax(),
                ExpenseParseError.InvalidAmount => new RegisterExpenseError.InvalidAmount(),
                ExpenseParseError.MissingDescription => new RegisterExpenseError.MissingDescription(),
                ExpenseParseError.UnknownCategory unknown => new RegisterExpenseError.UnknownCategory(unknown.Text),
                _ => throw new InvalidOperationException("Unknown expense parse error.")
            });
        }

        var input = (ExpenseParseResult.Success)parsed;
        CompraCreationResult creation = Compra.Criar(input.Amount, input.Description, input.Category, messageDateUtc.UtcDateTime);
        if (creation is CompraCreationResult.Failure invalid)
        {
            return new RegisterExpenseResult.Failure(invalid.Error switch
            {
                CompraError.InvalidAmount => new RegisterExpenseError.InvalidAmount(),
                CompraError.MissingDescription => new RegisterExpenseError.MissingDescription(),
                CompraError.InvalidCategory => new RegisterExpenseError.UnknownCategory(input.Category.ToString()),
                _ => throw new InvalidOperationException("Unknown expense domain error.")
            });
        }

        Compra expense = ((CompraCreationResult.Success)creation).Compra;
        await _expenseRepository.AddAsync(expense, cancellationToken);

        LimiteCategoria? limit = await _categoryLimitRepository.GetByCategoryAsync(expense.Categoria, cancellationToken);
        BillingCycle currentCycle = BillingCycle.GetBillingCycle(DateTimeConverter.GetBusinessDate(expense.Data));
        var (currentStartUtc, currentEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(currentCycle);
        IReadOnlyList<Compra> currentExpenses = await _expenseRepository.ListByPeriodAsync(
            currentStartUtc, currentEndExclusiveUtc, cancellationToken);

        CategoryLimitProgress? progress = limit?.AvaliarProgresso(
            currentExpenses.Where(item => item.Categoria == expense.Categoria).Sum(item => item.Valor));

        BillingCycle previousCycle = BillingCycle.GetBillingCycle(currentCycle.StartDate.AddMonths(-1));
        var (previousStartUtc, previousEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(previousCycle);
        IReadOnlyList<Compra> previousExpenses = await _expenseRepository.ListByPeriodAsync(
            previousStartUtc, previousEndExclusiveUtc, cancellationToken);

        return new RegisterExpenseResult.Success(ExpenseItem.From(expense), progress,
            PrepareAlert(expense, currentCycle, previousCycle, currentExpenses, previousExpenses));
    }

    private static SpendingPaceAlert? PrepareAlert(
        Compra expense, BillingCycle currentCycle, BillingCycle previousCycle,
        IReadOnlyList<Compra> currentExpenses, IReadOnlyList<Compra> previousExpenses)
    {
        double currentTotal = currentExpenses.Sum(item => item.Valor);
        double previousTotal = previousExpenses.Sum(item => item.Valor);
        SpendingAlertLevel? level = SpendingAlertPolicy.GetCrossedLevel(
            previousTotal, Math.Max(0, currentTotal - expense.Valor), currentTotal);
        if (level is null)
            return null;

        var categories = new List<CategorySpendingComparison>();
        foreach (Categoria category in CategoryCatalog.GetAll())
        {
            double current = currentExpenses.Where(item => item.Categoria == category).Sum(item => item.Valor);
            double previous = previousExpenses.Where(item => item.Categoria == category).Sum(item => item.Valor);
            SpendingComparison comparison = previous <= 0
                ? current <= 0 ? new SpendingComparison.NoSpending() : new SpendingComparison.NoHistory()
                : new SpendingComparison.Comparable((int)Math.Round(current / previous * 100), current - previous);
            categories.Add(new CategorySpendingComparison(category, current, previous, comparison));
        }

        return new SpendingPaceAlert(level.Value, currentCycle, previousCycle, currentTotal, previousTotal,
            (int)Math.Round(currentTotal / previousTotal * 100), currentTotal - previousTotal, categories);
    }
}
