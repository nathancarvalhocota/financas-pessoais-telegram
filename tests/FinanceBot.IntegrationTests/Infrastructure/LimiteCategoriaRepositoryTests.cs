using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace FinanceBot.IntegrationTests.Infrastructure;

public sealed class LimiteCategoriaRepositoryTests
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
            var repository = new LimiteCategoriaRepository(context);
            var limit = CreateLimit(Categoria.Mercado, 800.25);
            await repository.UpsertAsync(limit, CancellationToken.None);
            id = limit.Id;
            Assert.True(id > 0);
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            var repository = new LimiteCategoriaRepository(context);
            LimiteCategoria limit = Assert.IsType<LimiteCategoria>(await repository.GetByCategoryAsync(Categoria.Mercado, CancellationToken.None));
            Assert.Equal(id, limit.Id);
            Assert.Equal(Categoria.Mercado, limit.Categoria);
            Assert.Equal(800.25, limit.Valor);
            Assert.Empty(context.ChangeTracker.Entries());

            await repository.UpsertAsync(CreateLimit(Categoria.Mercado, 900.75), CancellationToken.None);
            await repository.UpsertAsync(CreateLimit(Categoria.Uber, 200.50), CancellationToken.None);
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            var repository = new LimiteCategoriaRepository(context);
            LimiteCategoria updated = Assert.IsType<LimiteCategoria>(await repository.GetByCategoryAsync(Categoria.Mercado, CancellationToken.None));
            Assert.Equal(id, updated.Id);
            Assert.Equal(Categoria.Mercado, updated.Categoria);
            Assert.Equal(900.75, updated.Valor);
            Assert.Equal(2, await context.LimiteCategorias.CountAsync());

            Assert.Equal(LimiteCategoriaError.InvalidValue, updated.AlterarValor(0));
            Assert.Equal(900.75, updated.Valor);
            await repository.DeleteAsync(updated, CancellationToken.None);
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            Assert.Null(await new LimiteCategoriaRepository(context).GetByCategoryAsync(Categoria.Mercado, CancellationToken.None));
            Assert.Equal(Categoria.Uber, Assert.Single(context.LimiteCategorias).Categoria);
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
            LimiteCategoria legacy = CreateLimit(Categoria.Mercado, 800);
            context.LimiteCategorias.Add(legacy);
            // Simulate a row written before entity validation existed.
            context.Entry(legacy).Property(limit => limit.Categoria).CurrentValue = (Categoria)999;
            await context.SaveChangesAsync();
            id = legacy.Id;
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            var repository = new LimiteCategoriaRepository(context);
            LimiteCategoria legacy = Assert.IsType<LimiteCategoria>(await repository.GetByCategoryAsync((Categoria)999, CancellationToken.None));
            Assert.Equal(id, legacy.Id);
            Assert.Equal((Categoria)999, legacy.Categoria);
            Assert.Equal(800, legacy.Valor);
            Assert.Equal(LimiteCategoriaError.InvalidCategory, legacy.AlterarValor(900));
            Assert.Equal(800, legacy.Valor);
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
        IEntityType entity = context.Model.FindEntityType(typeof(LimiteCategoria))!;
        var table = StoreObjectIdentifier.Table("limite_categorias", null);

        Assert.Equal("limite_categorias", entity.GetTableName());
        Assert.Equal(new[] { nameof(LimiteCategoria.Id) }, entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("id", entity.FindProperty(nameof(LimiteCategoria.Id))!.GetColumnName(table));
        Assert.Equal(ValueGenerated.OnAdd, entity.FindProperty(nameof(LimiteCategoria.Id))!.ValueGenerated);
        Assert.Equal("categoria", entity.FindProperty(nameof(LimiteCategoria.Categoria))!.GetColumnName(table));
        Assert.Equal(50, entity.FindProperty(nameof(LimiteCategoria.Categoria))!.GetMaxLength());
        Assert.Equal(typeof(string), entity.FindProperty(nameof(LimiteCategoria.Categoria))!.GetTypeMapping().Converter!.ProviderClrType);
        IProperty value = entity.FindProperty(nameof(LimiteCategoria.Valor))!;
        Assert.Equal("valor", value.GetColumnName(table));
        Assert.Equal(typeof(double), value.ClrType);
        Assert.Equal("double precision", value.GetColumnType());
        Assert.False(value.IsNullable);
        IIndex index = Assert.Single(entity.GetIndexes());
        Assert.True(index.IsUnique);
        Assert.Equal(nameof(LimiteCategoria.Categoria), Assert.Single(index.Properties).Name);
        Assert.Equal(3, entity.GetProperties().Count());
    }

    private static LimiteCategoria CreateLimit(Categoria category, double value) =>
        Assert.IsType<LimiteCategoriaCreationResult.Success>(LimiteCategoria.Criar(category, value)).Limite;
}
