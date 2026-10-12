namespace FinanceBot.Domain.Expenses;

public interface ICompraRepository
{
    Task AddAsync(Compra compra, CancellationToken cancellationToken);

    /// <summary>Lists expenses where Data &gt;= periodStartUtc and Data &lt; periodEndExclusiveUtc, newest first.</summary>
    Task<IReadOnlyList<Compra>> ListByPeriodAsync(
        DateTime periodStartUtc,
        DateTime periodEndExclusiveUtc,
        CancellationToken cancellationToken);

    Task<Compra?> FindByIdAsync(int compraId, CancellationToken cancellationToken);

    Task DeleteAsync(Compra compra, CancellationToken cancellationToken);
}
