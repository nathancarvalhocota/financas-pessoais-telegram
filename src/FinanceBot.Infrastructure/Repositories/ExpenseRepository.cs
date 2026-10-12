using FinanceBot.Domain.Expenses;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Repositories;

public sealed class ExpenseRepository : IExpenseRepository
{
    private readonly FinanceBotDbContext _financeBotDbContext;

    public ExpenseRepository(FinanceBotDbContext financeBotDbContext)
    {
        _financeBotDbContext = financeBotDbContext;
    }

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken)
    {
        await _financeBotDbContext.Expenses.AddAsync(expense, cancellationToken);
        await _financeBotDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> ListByPeriodAsync(
        DateTime periodStartUtc,
        DateTime periodEndExclusiveUtc,
        CancellationToken cancellationToken)
    {
        List<Expense> expenses = await _financeBotDbContext.Expenses
            .AsNoTracking()
            .Where(expense => expense.DateUtc >= periodStartUtc && expense.DateUtc < periodEndExclusiveUtc)
            .OrderByDescending(expense => expense.DateUtc)
            .ToListAsync(cancellationToken);

        return expenses;
    }

    public async Task<Expense?> FindByIdAsync(int expenseId, CancellationToken cancellationToken)
    {
        Expense? expense = await _financeBotDbContext.Expenses
            .FirstOrDefaultAsync(expense => expense.Id == expenseId, cancellationToken);

        return expense;
    }

    public async Task DeleteAsync(Expense expense, CancellationToken cancellationToken)
    {
        _financeBotDbContext.Expenses.Remove(expense);
        await _financeBotDbContext.SaveChangesAsync(cancellationToken);
    }
}
