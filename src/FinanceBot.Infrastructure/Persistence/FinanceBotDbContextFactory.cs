using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FinanceBot.Infrastructure.Persistence;

public sealed class FinanceBotDbContextFactory : IDesignTimeDbContextFactory<FinanceBotDbContext>
{
    public FinanceBotDbContext CreateDbContext(string[] args)
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            ConfigurationManager configurationManager = new ConfigurationManager();
            configurationManager.AddUserSecrets<FinanceBotDbContextFactory>(optional: true);
            connectionString = configurationManager.GetConnectionString("DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:DefaultConnection' was not found. " +
                "Set it in user-secrets or environment variables before running migrations. " +
                "Example env var: $env:ConnectionStrings__DefaultConnection = " +
                "\"Host=localhost;Port=5432;Database=financebot;Username=postgres;Password=postgres\"");
        }

        DbContextOptionsBuilder<FinanceBotDbContext> contextOptionsBuilder =
            new DbContextOptionsBuilder<FinanceBotDbContext>();
        contextOptionsBuilder.UseNpgsql(connectionString);

        FinanceBotDbContext financeBotDbContext = new FinanceBotDbContext(contextOptionsBuilder.Options);
        return financeBotDbContext;
    }
}
