using FinanceBot.Api.Commands.Parsing;
using FinanceBot.Api.Commands.Results;
using FinanceBot.Domain.Expenses;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Billing;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Handlers;

public sealed class RegisterExpenseCommandHandler
{
    private readonly IExpenseRepository _expenseRepository;
    private readonly ICategoryLimitRepository _categoryLimitRepository;

    public RegisterExpenseCommandHandler(
        IExpenseRepository expenseRepository,
        ICategoryLimitRepository categoryLimitRepository)
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
        ExpenseCreationResult creation = Expense.Create(input.Amount, input.Description, input.Category, messageDateUtc.UtcDateTime);
        if (creation is ExpenseCreationResult.Failure invalid)
        {
            return new RegisterExpenseResult.Failure(invalid.Error switch
            {
                ExpenseError.InvalidAmount => new RegisterExpenseError.InvalidAmount(),
                ExpenseError.MissingDescription => new RegisterExpenseError.MissingDescription(),
                ExpenseError.InvalidCategory => new RegisterExpenseError.UnknownCategory(input.Category.ToString()),
                _ => throw new InvalidOperationException("Unknown expense domain error.")
            });
        }

        Expense expense = ((ExpenseCreationResult.Success)creation).Expense;
        await _expenseRepository.AddAsync(expense, cancellationToken);

        CategoryLimit? limit = await _categoryLimitRepository.GetByCategoryAsync(expense.Category, cancellationToken);
        BillingCycle currentCycle = BillingCycle.GetBillingCycle(DateTimeConverter.GetBusinessDate(expense.DateUtc));
        var (currentStartUtc, currentEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(currentCycle);
        IReadOnlyList<Expense> currentExpenses = await _expenseRepository.ListByPeriodAsync(
            currentStartUtc, currentEndExclusiveUtc, cancellationToken);

        CategoryLimitProgress? progress = limit?.EvaluateProgress(
            currentExpenses.Where(item => item.Category == expense.Category).Sum(item => item.Amount));

        BillingCycle previousCycle = BillingCycle.GetBillingCycle(currentCycle.StartDate.AddMonths(-1));
        var (previousStartUtc, previousEndExclusiveUtc) = DateTimeConverter.GetUtcPeriod(previousCycle);
        IReadOnlyList<Expense> previousExpenses = await _expenseRepository.ListByPeriodAsync(
            previousStartUtc, previousEndExclusiveUtc, cancellationToken);

        return new RegisterExpenseResult.Success(ExpenseItem.From(expense), progress,
            PrepareAlert(expense, currentCycle, previousCycle, currentExpenses, previousExpenses));
    }

    private static SpendingPaceAlert? PrepareAlert(
        Expense expense, BillingCycle currentCycle, BillingCycle previousCycle,
        IReadOnlyList<Expense> currentExpenses, IReadOnlyList<Expense> previousExpenses)
    {
        double currentTotal = currentExpenses.Sum(item => item.Amount);
        double previousTotal = previousExpenses.Sum(item => item.Amount);
        SpendingAlertLevel? level = SpendingAlertPolicy.GetCrossedLevel(
            previousTotal, Math.Max(0, currentTotal - expense.Amount), currentTotal);
        if (level is null)
            return null;

        var categories = new List<CategorySpendingComparison>();
        foreach (Category category in CategoryCatalog.GetAll())
        {
            double current = currentExpenses.Where(item => item.Category == category).Sum(item => item.Amount);
            double previous = previousExpenses.Where(item => item.Category == category).Sum(item => item.Amount);
            SpendingComparison comparison;
            if (previous <= 0 && current <= 0)
            {
                comparison = new SpendingComparison.NoSpending();
            }
            else if (previous <= 0)
            {
                comparison = new SpendingComparison.NoHistory();
            }
            else
            {
                comparison = new SpendingComparison.Comparable((int)Math.Round(current / previous * 100), current - previous);
            }
            categories.Add(new CategorySpendingComparison(category, current, previous, comparison));
        }

        return new SpendingPaceAlert(level.Value, currentCycle, previousCycle, currentTotal, previousTotal,
            (int)Math.Round(currentTotal / previousTotal * 100), currentTotal - previousTotal, categories);
    }
}
