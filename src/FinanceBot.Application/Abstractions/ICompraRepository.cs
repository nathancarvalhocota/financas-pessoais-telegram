using FinanceBot.Domain.Entities;

namespace FinanceBot.Application.Abstractions;

public interface ICompraRepository
{
    Task AddAsync(Compra compra, CancellationToken cancellationToken);

    Task<IReadOnlyList<Compra>> ListByPeriodAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken);

    Task<Compra?> FindByIdAsync(int compraId, CancellationToken cancellationToken);

    Task DeleteAsync(Compra compra, CancellationToken cancellationToken);
}
