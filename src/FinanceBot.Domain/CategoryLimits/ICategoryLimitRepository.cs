using FinanceBot.Domain.Categories;

namespace FinanceBot.Domain.CategoryLimits;

public interface ICategoryLimitRepository
{
    Task<CategoryLimit?> GetByCategoryAsync(Category category, CancellationToken cancellationToken);

    Task UpsertAsync(CategoryLimit categoryLimit, CancellationToken cancellationToken);

    Task DeleteAsync(CategoryLimit categoryLimit, CancellationToken cancellationToken);
}
