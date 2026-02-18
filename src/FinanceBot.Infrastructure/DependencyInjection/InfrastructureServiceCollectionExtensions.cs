using FinanceBot.Application.Abstractions;
using FinanceBot.Infrastructure.Telegram;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceBot.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<TelegramOptions>(
            configuration.GetSection(TelegramOptions.SectionName));
        services.AddHttpClient<ITelegramMessageSender, TelegramMessageSender>();

        return services;
    }
}
