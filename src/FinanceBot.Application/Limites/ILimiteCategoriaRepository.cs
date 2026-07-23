using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;

namespace FinanceBot.Application.Limites;

public interface ILimiteCategoriaRepository
{
    Task<LimiteCategoria?> GetByCategoryAsync(Categoria categoria, CancellationToken cancellationToken);

    Task UpsertAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken);

    Task DeleteAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken);
}
