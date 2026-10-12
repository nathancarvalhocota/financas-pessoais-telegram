using FinanceBot.Domain.Categories;
using FinanceBot.Domain.Expenses;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace FinanceBot.IntegrationTests.Infrastructure;

public sealed class ExpenseRepositoryTests
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
            Expense expense = Assert.IsType<ExpenseCreationResult.Success>(Expense.Create(58.90, "Almoco", Category.Mercado, date)).Expense;
            await new ExpenseRepository(context).AddAsync(expense, CancellationToken.None);
            id = expense.Id;
            Assert.True(id > 0);
        }

        await using (var context = new FinanceBotDbContext(options))
        {
            var repository = new ExpenseRepository(context);
            Expense expense = Assert.IsType<Expense>(await repository.FindByIdAsync(id, CancellationToken.None));
            Assert.Equal(id, expense.Id);
            Assert.Equal(58.90, expense.Amount);
            Assert.Equal("Almoco", expense.Description);
            Assert.Equal(Category.Mercado, expense.Category);
            Assert.Equal(date, expense.DateUtc);
            Assert.Equal(DateTimeKind.Utc, expense.DateUtc.Kind);
            await repository.DeleteAsync(expense, CancellationToken.None);
        }

        await using var verify = new FinanceBotDbContext(options);
        Assert.Empty(verify.Expenses);
    }

    [Fact]
    public async Task Materialization_LegacyInvalidData_DoesNotTransformExistingFields()
    {
        var options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        int id;
        await using (var context = new FinanceBotDbContext(options))
        {
            Expense legacy = ExpenseAt(DateTime.UtcNow);
            context.Expenses.Add(legacy);
            context.Entry(legacy).Property(item => item.Amount).CurrentValue = -1;
            context.Entry(legacy).Property(item => item.Description).CurrentValue = "";
            context.Entry(legacy).Property(item => item.Category).CurrentValue = (Category)999;
            await context.SaveChangesAsync();
            id = legacy.Id;
        }

        await using var read = new FinanceBotDbContext(options);
        Expense materialized = Assert.IsType<Expense>(await new ExpenseRepository(read).FindByIdAsync(id, CancellationToken.None));
        Assert.Equal(id, materialized.Id);
        Assert.Equal(-1, materialized.Amount);
        Assert.Equal("", materialized.Description);
        Assert.Equal((Category)999, materialized.Category);
        Assert.Equal(EntityState.Unchanged, read.Entry(materialized).State);
    }

    [Fact]
    public void Mapping_PreservesPostgreSqlSchemaWithoutDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options;
        using var context = new FinanceBotDbContext(options);
        IEntityType entity = context.Model.FindEntityType(typeof(Expense))!;
        var table = StoreObjectIdentifier.Table("compras", null);

        Assert.Equal("compras", entity.GetTableName());
        Assert.Equal(new[] { nameof(Expense.Id) }, entity.FindPrimaryKey()!.Properties.Select(item => item.Name));
        Assert.Equal(ValueGenerated.OnAdd, entity.FindProperty(nameof(Expense.Id))!.ValueGenerated);
        Assert.Equal("id", entity.FindProperty(nameof(Expense.Id))!.GetColumnName(table));
        IProperty value = entity.FindProperty(nameof(Expense.Amount))!;
        Assert.Equal("valor", value.GetColumnName(table));
        Assert.Equal(typeof(double), value.ClrType);
        Assert.Equal("double precision", value.GetColumnType());
        IProperty description = entity.FindProperty(nameof(Expense.Description))!;
        Assert.Equal("descricao", description.GetColumnName(table));
        Assert.Equal(255, description.GetMaxLength());
        IProperty category = entity.FindProperty(nameof(Expense.Category))!;
        Assert.Equal("categoria", category.GetColumnName(table));
        Assert.Equal(50, category.GetMaxLength());
        Assert.Equal(typeof(string), category.GetTypeMapping().Converter!.ProviderClrType);
        IProperty date = entity.FindProperty(nameof(Expense.DateUtc))!;
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
        Expense beforeStart = ExpenseAt(startUtc.AddTicks(-1));
        Expense atStart = ExpenseAt(startUtc);
        Expense lastInstant = ExpenseAt(endExclusiveUtc.AddTicks(-1));
        Expense atEnd = ExpenseAt(endExclusiveUtc);
        context.Expenses.AddRange(atStart, atEnd, beforeStart, lastInstant);
        await context.SaveChangesAsync();
        ExpenseRepository repository = new ExpenseRepository(context);

        IReadOnlyList<Expense> expenses = await repository.ListByPeriodAsync(startUtc, endExclusiveUtc, CancellationToken.None);

        Assert.Equal(new[] { lastInstant.Id, atStart.Id }, expenses.Select(expense => expense.Id));
        Assert.All(expenses, expense => Assert.Equal(DateTimeKind.Utc, expense.DateUtc.Kind));
    }

    private static Expense ExpenseAt(DateTime dateUtc) => Assert.IsType<ExpenseCreationResult.Success>(Expense.Create(10, "Compra na fronteira", Category.Mercado, dateUtc)).Expense;
}
