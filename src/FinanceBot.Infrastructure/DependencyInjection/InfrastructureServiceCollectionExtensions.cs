using FinanceBot.Domain.Expenses;
using FinanceBot.Domain.CategoryLimits;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace FinanceBot.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:DefaultConnection' was not found. " +
                "Configure it via user-secrets, environment variables, or appsettings.");
        }

        services.AddDbContext<FinanceBotDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<ICategoryLimitRepository, CategoryLimitRepository>();

        return services;
    }
}
