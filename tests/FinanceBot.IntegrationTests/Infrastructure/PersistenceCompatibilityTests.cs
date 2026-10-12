using FinanceBot.Domain.Categories;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Domain.Expenses;
using FinanceBot.Infrastructure.Migrations;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FinanceBot.IntegrationTests.Infrastructure;

public sealed class PersistenceCompatibilityTests
{
    [Fact]
    public void EnglishIdentifiers_RequireNoSchemaChangesAndMatchModelSnapshot()
    {
        using var context = CreateContext();
        IModel currentModel = context.GetService<IDesignTimeModel>().Model;
        var initializer = context.GetService<IModelRuntimeInitializer>();
        var differ = context.GetService<IMigrationsModelDiffer>();
        IModel[] previousModels =
        {
            new AddLimiteCategoria().TargetModel,
            context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model
        };

        foreach (IModel previousModel in previousModels)
        {
            IModel initializedModel = initializer.Initialize(previousModel, designTime: true);
            Assert.Empty(differ.GetDifferences(
                initializedModel.GetRelationalModel(), currentModel.GetRelationalModel()));
        }
    }

    [Theory]
    [InlineData(Category.Educacao, "Educacao")]
    [InlineData(Category.Lazer, "Lazer")]
    [InlineData(Category.Lanches, "Lanches")]
    [InlineData(Category.Uber, "Uber")]
    [InlineData(Category.Mercado, "Mercado")]
    [InlineData(Category.Moto, "Moto")]
    [InlineData(Category.Compras, "Compras")]
    [InlineData(Category.Outros, "Outros")]
    [InlineData(Category.Estetica, "Estetica")]
    [InlineData(Category.Limpeza, "Limpeza")]
    [InlineData(Category.Saude, "Saude")]
    public void CategoryConversion_ReadsAndWritesExistingPortugueseValues(Category category, string storedValue)
    {
        using var context = CreateContext();

        foreach (Type entityType in new[] { typeof(Expense), typeof(CategoryLimit) })
        {
            var converter = context.Model.FindEntityType(entityType)!
                .FindProperty(nameof(Expense.Category))!.GetTypeMapping().Converter!;

            Assert.Equal(storedValue, converter.ConvertToProvider(category));
            Assert.Equal(category, converter.ConvertFromProvider(storedValue));
        }
    }

    private static FinanceBotDbContext CreateContext() => new(
        new DbContextOptionsBuilder<FinanceBotDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options);
}
