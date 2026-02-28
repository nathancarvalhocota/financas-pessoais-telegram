using FinanceBot.Application.Abstractions;
using FinanceBot.Domain.Entities;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Repositories;

public sealed class CompraRepository : ICompraRepository
{
    private readonly FinanceBotDbContext _financeBotDbContext;

    public CompraRepository(FinanceBotDbContext financeBotDbContext)
    {
        _financeBotDbContext = financeBotDbContext;
    }

    public async Task AddAsync(Compra compra, CancellationToken cancellationToken)
    {
        await _financeBotDbContext.Compras.AddAsync(compra, cancellationToken);
        await _financeBotDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Compra>> ListByPeriodAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken)
    {
        List<Compra> compras = await _financeBotDbContext.Compras
            .AsNoTracking()
            .Where(compra => compra.Data >= periodStartUtc && compra.Data < periodEndUtc)
            .OrderByDescending(compra => compra.Data)
            .ToListAsync(cancellationToken);

        return compras;
    }

    public async Task<Compra?> FindByIdAsync(int compraId, CancellationToken cancellationToken)
    {
        Compra? compra = await _financeBotDbContext.Compras
            .FirstOrDefaultAsync(compra => compra.Id == compraId, cancellationToken);

        return compra;
    }

    public async Task DeleteAsync(Compra compra, CancellationToken cancellationToken)
    {
        _financeBotDbContext.Compras.Remove(compra);
        await _financeBotDbContext.SaveChangesAsync(cancellationToken);
    }
}
