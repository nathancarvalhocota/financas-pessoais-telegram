using FinanceBot.Application.Abstractions;
using FinanceBot.Domain.Entities;
using FinanceBot.Domain.Enums;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Repositories;

public sealed class LimiteCategoriaRepository : ILimiteCategoriaRepository
{
    private readonly FinanceBotDbContext _financeBotDbContext;

    public LimiteCategoriaRepository(FinanceBotDbContext financeBotDbContext)
    {
        _financeBotDbContext = financeBotDbContext;
    }

    public async Task<LimiteCategoria?> GetByCategoriaAsync(
        Categoria categoria,
        CancellationToken cancellationToken)
    {
        return await _financeBotDbContext.LimiteCategorias
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Categoria == categoria, cancellationToken);
    }

    public async Task UpsertAsync(
        LimiteCategoria limiteCategoria,
        CancellationToken cancellationToken)
    {
        LimiteCategoria? existing = await _financeBotDbContext.LimiteCategorias
            .FirstOrDefaultAsync(l => l.Categoria == limiteCategoria.Categoria, cancellationToken);

        if (existing is null)
        {
            await _financeBotDbContext.LimiteCategorias.AddAsync(limiteCategoria, cancellationToken);
        }
        else
        {
            existing.Valor = limiteCategoria.Valor;
        }

        await _financeBotDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        LimiteCategoria limiteCategoria,
        CancellationToken cancellationToken)
    {
        _financeBotDbContext.LimiteCategorias.Remove(limiteCategoria);
        await _financeBotDbContext.SaveChangesAsync(cancellationToken);
    }
}
