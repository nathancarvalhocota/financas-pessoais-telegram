using FinanceBot.Infrastructure.Persistence;
using Xunit;

namespace FinanceBot.UnitTests.Infrastructure;

public sealed class FinanceBotDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithEnvironmentConnectionString_ReturnsNpgsqlContext()
    {
        string? originalConnectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection");
        string environmentConnectionString =
            "Host=localhost;Port=5432;Database=financebot_env_test;Username=postgres;Password=postgres";

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            environmentConnectionString);
        try
        {
            FinanceBotDbContextFactory financeBotDbContextFactory = new FinanceBotDbContextFactory();
            FinanceBotDbContext financeBotDbContext = financeBotDbContextFactory.CreateDbContext(
                Array.Empty<string>());

            Assert.NotNull(financeBotDbContext);
            string providerName = Assert.IsType<string>(financeBotDbContext.Database.ProviderName);
            Assert.Contains("Npgsql", providerName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "ConnectionStrings__DefaultConnection",
                originalConnectionString);
        }
    }
}
