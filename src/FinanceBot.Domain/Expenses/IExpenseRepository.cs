namespace FinanceBot.Domain.Expenses;

public interface IExpenseRepository
{
    Task AddAsync(Expense expense, CancellationToken cancellationToken);

    /// <summary>Lists expenses where DateUtc &gt;= periodStartUtc and DateUtc &lt; periodEndExclusiveUtc, newest first.</summary>
    Task<IReadOnlyList<Expense>> ListByPeriodAsync(
        DateTime periodStartUtc,
        DateTime periodEndExclusiveUtc,
        CancellationToken cancellationToken);

    Task<Expense?> FindByIdAsync(int expenseId, CancellationToken cancellationToken);

    Task DeleteAsync(Expense expense, CancellationToken cancellationToken);
}
