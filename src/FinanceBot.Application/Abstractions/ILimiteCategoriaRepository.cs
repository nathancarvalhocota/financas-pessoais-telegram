using FinanceBot.Domain.Entities;
using FinanceBot.Domain.Enums;

namespace FinanceBot.Application.Abstractions;

public interface ILimiteCategoriaRepository
{
    Task<LimiteCategoria?> GetByCategoriaAsync(Categoria categoria, CancellationToken cancellationToken);

    Task UpsertAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken);

    Task DeleteAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken);
}
