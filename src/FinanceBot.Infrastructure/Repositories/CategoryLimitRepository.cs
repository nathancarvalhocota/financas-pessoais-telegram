using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Categories;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.Repositories;

public sealed class CategoryLimitRepository : ICategoryLimitRepository
{
    private readonly FinanceBotDbContext _financeBotDbContext;

    public CategoryLimitRepository(FinanceBotDbContext financeBotDbContext)
    {
        _financeBotDbContext = financeBotDbContext;
    }

    public async Task<CategoryLimit?> GetByCategoryAsync(
        Category category,
        CancellationToken cancellationToken)
    {
        return await _financeBotDbContext.CategoryLimits
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Category == category, cancellationToken);
    }

    public async Task UpsertAsync(
        CategoryLimit categoryLimit,
        CancellationToken cancellationToken)
    {
        CategoryLimit? existing = await _financeBotDbContext.CategoryLimits
            .FirstOrDefaultAsync(l => l.Category == categoryLimit.Category, cancellationToken);

        if (existing is null)
        {
            await _financeBotDbContext.CategoryLimits.AddAsync(categoryLimit, cancellationToken);
        }
        else
        {
            CategoryLimitValidationError? error = existing.UpdateAmount(categoryLimit.Amount);
            if (error is not null)
            {
                throw new InvalidOperationException($"Invalid category limit update: {error}.");
            }
        }

        await _financeBotDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        CategoryLimit categoryLimit,
        CancellationToken cancellationToken)
    {
        _financeBotDbContext.CategoryLimits.Remove(categoryLimit);
        await _financeBotDbContext.SaveChangesAsync(cancellationToken);
    }
}
