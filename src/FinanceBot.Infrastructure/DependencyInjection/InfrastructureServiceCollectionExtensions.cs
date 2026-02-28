using FinanceBot.Application.Abstractions;
using FinanceBot.Infrastructure.Persistence;
using FinanceBot.Infrastructure.Repositories;
using FinanceBot.Infrastructure.Telegram;
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

        services.Configure<TelegramOptions>(
            configuration.GetSection(TelegramOptions.SectionName));
        services.AddHttpClient<ITelegramMessageSender, TelegramMessageSender>();
        services.AddScoped<ICompraRepository, CompraRepository>();

        return services;
    }
}
