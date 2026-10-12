using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace FinanceBot.IntegrationTests.Infrastructure;

public sealed class CategoryLimitRepositoryTests
{
    [Fact]
    public async Task Upsert_InsertMaterializeAndUpdate_PreservesIdentityAndCategory()
    {
        DbContextOptions<FinanceBotDbContext> options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        int id;
        await using (var context = new FinanceBotDbContext(options))
        {
            var repository = new CategoryLimitRepository(context);
            var limit = CreateLimit(Category.Mercado, 800.25);
            await repository.UpsertAsync(limit, CancellationToken.None);
            id = limit.Id;
            Assert.True(id > 0);
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            var repository = new CategoryLimitRepository(context);
            CategoryLimit limit = Assert.IsType<CategoryLimit>(await repository.GetByCategoryAsync(Category.Mercado, CancellationToken.None));
            Assert.Equal(id, limit.Id);
            Assert.Equal(Category.Mercado, limit.Category);
            Assert.Equal(800.25, limit.Amount);
            Assert.Empty(context.ChangeTracker.Entries());

            await repository.UpsertAsync(CreateLimit(Category.Mercado, 900.75), CancellationToken.None);
            await repository.UpsertAsync(CreateLimit(Category.Uber, 200.50), CancellationToken.None);
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            var repository = new CategoryLimitRepository(context);
            CategoryLimit updated = Assert.IsType<CategoryLimit>(await repository.GetByCategoryAsync(Category.Mercado, CancellationToken.None));
            Assert.Equal(id, updated.Id);
            Assert.Equal(Category.Mercado, updated.Category);
            Assert.Equal(900.75, updated.Amount);
            Assert.Equal(2, await context.CategoryLimits.CountAsync());

            Assert.Equal(CategoryLimitValidationError.InvalidValue, updated.UpdateAmount(0));
            Assert.Equal(900.75, updated.Amount);
            await repository.DeleteAsync(updated, CancellationToken.None);
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            Assert.Null(await new CategoryLimitRepository(context).GetByCategoryAsync(Category.Mercado, CancellationToken.None));
            Assert.Equal(Category.Uber, Assert.Single(context.CategoryLimits).Category);
        }
    }

    [Fact]
    public async Task Materialization_LegacyInvalidCategory_DoesNotCleanDataAndControlledUpdateRejectsIt()
    {
        var options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        int id;
        await using (var context = new FinanceBotDbContext(options))
        {
            CategoryLimit legacy = CreateLimit(Category.Mercado, 800);
            context.CategoryLimits.Add(legacy);
            // Simulate a row written before entity validation existed.
            context.Entry(legacy).Property(limit => limit.Category).CurrentValue = (Category)999;
            await context.SaveChangesAsync();
            id = legacy.Id;
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            var repository = new CategoryLimitRepository(context);
            CategoryLimit legacy = Assert.IsType<CategoryLimit>(await repository.GetByCategoryAsync((Category)999, CancellationToken.None));
            Assert.Equal(id, legacy.Id);
            Assert.Equal((Category)999, legacy.Category);
            Assert.Equal(800, legacy.Amount);
            Assert.Equal(CategoryLimitValidationError.InvalidCategory, legacy.UpdateAmount(900));
            Assert.Equal(800, legacy.Amount);
            Assert.Empty(context.ChangeTracker.Entries());
        }
    }

    [Fact]
    public void Mapping_PreservesPostgreSqlSchemaWithoutConnectingToDatabase()
    {
        var options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var context = new FinanceBotDbContext(options);
        IEntityType entity = context.Model.FindEntityType(typeof(CategoryLimit))!;
        var table = StoreObjectIdentifier.Table("limite_categorias", null);

        Assert.Equal("limite_categorias", entity.GetTableName());
        Assert.Equal(new[] { nameof(CategoryLimit.Id) }, entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("id", entity.FindProperty(nameof(CategoryLimit.Id))!.GetColumnName(table));
        Assert.Equal(ValueGenerated.OnAdd, entity.FindProperty(nameof(CategoryLimit.Id))!.ValueGenerated);
        Assert.Equal("categoria", entity.FindProperty(nameof(CategoryLimit.Category))!.GetColumnName(table));
        Assert.Equal(50, entity.FindProperty(nameof(CategoryLimit.Category))!.GetMaxLength());
        Assert.Equal(typeof(string), entity.FindProperty(nameof(CategoryLimit.Category))!.GetTypeMapping().Converter!.ProviderClrType);
        IProperty value = entity.FindProperty(nameof(CategoryLimit.Amount))!;
        Assert.Equal("valor", value.GetColumnName(table));
        Assert.Equal(typeof(double), value.ClrType);
        Assert.Equal("double precision", value.GetColumnType());
        Assert.False(value.IsNullable);
        IIndex index = Assert.Single(entity.GetIndexes());
        Assert.True(index.IsUnique);
        Assert.Equal(nameof(CategoryLimit.Category), Assert.Single(index.Properties).Name);
        Assert.Equal(3, entity.GetProperties().Count());
    }

    private static CategoryLimit CreateLimit(Category category, double value) =>
        Assert.IsType<CategoryLimitCreationResult.Success>(CategoryLimit.Create(category, value)).Limit;
}
