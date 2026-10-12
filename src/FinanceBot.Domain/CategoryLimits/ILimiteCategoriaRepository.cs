using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.CategoryLimits;

public interface ILimiteCategoriaRepository
{
    Task<LimiteCategoria?> GetByCategoryAsync(Categoria categoria, CancellationToken cancellationToken);

    Task UpsertAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken);

    Task DeleteAsync(LimiteCategoria limiteCategoria, CancellationToken cancellationToken);
}
