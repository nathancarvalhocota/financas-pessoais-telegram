using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace FinanceBot.IntegrationTests.Infrastructure;

public sealed class CompraRepositoryTests
{
    [Fact]
    public async Task AddAndMaterializeInAnotherContext_PreservesIdentityAndAllFields()
    {
        var options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        DateTime date = new(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        int id;
        await using (var context = new FinanceBotDbContext(options))
        {
            Compra expense = Assert.IsType<CompraCreationResult.Success>(Compra.Criar(58.90, "Almoco", Categoria.Mercado, date)).Compra;
            await new CompraRepository(context).AddAsync(expense, CancellationToken.None);
            id = expense.Id;
            Assert.True(id > 0);
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            var repository = new CompraRepository(context);
            Compra expense = Assert.IsType<Compra>(await repository.FindByIdAsync(id, CancellationToken.None));
            Assert.Equal(id, expense.Id);
            Assert.Equal(58.90, expense.Valor);
            Assert.Equal("Almoco", expense.Descricao);
            Assert.Equal(Categoria.Mercado, expense.Categoria);
            Assert.Equal(date, expense.Data);
            Assert.Equal(DateTimeKind.Utc, expense.Data.Kind);
            await repository.DeleteAsync(expense, CancellationToken.None);
        }

        await using var verify = new FinanceBotDbContext(options);
        Assert.Empty(verify.Compras);
    }

    [Fact]
    public async Task Materialization_LegacyInvalidData_DoesNotTransformExistingFields()
    {
        var options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        int id;
        await using (var context = new FinanceBotDbContext(options))
        {
            Compra legacy = ExpenseAt(DateTime.UtcNow);
            context.Compras.Add(legacy);
            context.Entry(legacy).Property(item => item.Valor).CurrentValue = -1;
            context.Entry(legacy).Property(item => item.Descricao).CurrentValue = "";
            context.Entry(legacy).Property(item => item.Categoria).CurrentValue = (Categoria)999;
            await context.SaveChangesAsync();
            id = legacy.Id;
        }

        await using var read = new FinanceBotDbContext(options);
        Compra materialized = Assert.IsType<Compra>(await new CompraRepository(read).FindByIdAsync(id, CancellationToken.None));
        Assert.Equal(id, materialized.Id);
        Assert.Equal(-1, materialized.Valor);
        Assert.Equal("", materialized.Descricao);
        Assert.Equal((Categoria)999, materialized.Categoria);
        Assert.Equal(EntityState.Unchanged, read.Entry(materialized).State);
    }

    [Fact]
    public void Mapping_PreservesPostgreSqlSchemaWithoutDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;
        using var context = new FinanceBotDbContext(options);
        IEntityType entity = context.Model.FindEntityType(typeof(Compra))!;
        var table = StoreObjectIdentifier.Table("compras", null);

        Assert.Equal("compras", entity.GetTableName());
        Assert.Equal(new[] { nameof(Compra.Id) }, entity.FindPrimaryKey()!.Properties.Select(item => item.Name));
        Assert.Equal(ValueGenerated.OnAdd, entity.FindProperty(nameof(Compra.Id))!.ValueGenerated);
        Assert.Equal("id", entity.FindProperty(nameof(Compra.Id))!.GetColumnName(table));
        IProperty value = entity.FindProperty(nameof(Compra.Valor))!;
        Assert.Equal("valor", value.GetColumnName(table));
        Assert.Equal(typeof(double), value.ClrType);
        Assert.Equal("double precision", value.GetColumnType());
        IProperty description = entity.FindProperty(nameof(Compra.Descricao))!;
        Assert.Equal("descricao", description.GetColumnName(table));
        Assert.Equal(255, description.GetMaxLength());
        IProperty category = entity.FindProperty(nameof(Compra.Categoria))!;
        Assert.Equal("categoria", category.GetColumnName(table));
        Assert.Equal(50, category.GetMaxLength());
        Assert.Equal(typeof(string), category.GetTypeMapping().Converter!.ProviderClrType);
        IProperty date = entity.FindProperty(nameof(Compra.Data))!;
        Assert.Equal("data", date.GetColumnName(table));
        Assert.Equal("timestamp with time zone", date.GetColumnType());
        Assert.All(entity.GetProperties(), item => Assert.False(item.IsNullable));
        Assert.Equal(5, entity.GetProperties().Count());
    }

    [Fact]
    public async Task ListByPeriodAsync_IncludesStartExcludesEndAndOrdersNewestFirst()
    {
        DbContextOptions<FinanceBotDbContext> options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using FinanceBotDbContext context = new FinanceBotDbContext(options);
        DateTime startUtc = new DateTime(2025, 12, 15, 3, 0, 0, DateTimeKind.Utc);
        DateTime endExclusiveUtc = new DateTime(2026, 1, 15, 3, 0, 0, DateTimeKind.Utc);
        Compra beforeStart = ExpenseAt(startUtc.AddTicks(-1));
        Compra atStart = ExpenseAt(startUtc);
        Compra lastInstant = ExpenseAt(endExclusiveUtc.AddTicks(-1));
        Compra atEnd = ExpenseAt(endExclusiveUtc);
        context.Compras.AddRange(atStart, atEnd, beforeStart, lastInstant);
        await context.SaveChangesAsync();
        CompraRepository repository = new CompraRepository(context);

        IReadOnlyList<Compra> expenses = await repository.ListByPeriodAsync(startUtc, endExclusiveUtc, CancellationToken.None);

        Assert.Equal(new[] { lastInstant.Id, atStart.Id }, expenses.Select(expense => expense.Id));
        Assert.All(expenses, expense => Assert.Equal(DateTimeKind.Utc, expense.Data.Kind));
    }

    private static Compra ExpenseAt(DateTime dateUtc) => Assert.IsType<CompraCreationResult.Success>(Compra.Criar(10, "Compra na fronteira", Categoria.Mercado, dateUtc)).Compra;
}
