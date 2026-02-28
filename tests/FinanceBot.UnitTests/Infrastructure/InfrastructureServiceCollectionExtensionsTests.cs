using FinanceBot.Infrastructure.DependencyInjection;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinanceBot.UnitTests.Infrastructure;

public sealed class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_WithMissingConnectionString_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder().Build();

        InvalidOperationException invalidOperationException =
            Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));

        Assert.Contains("ConnectionStrings:DefaultConnection", invalidOperationException.Message);
    }

    [Fact]
    public void AddInfrastructure_WithConnectionString_RegistersFinanceBotDbContext()
    {
        ServiceCollection services = new ServiceCollection();
        Dictionary<string, string?> settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Host=localhost;Port=5432;Database=financebot_test;Username=postgres;Password=postgres",
            ["Telegram:BotToken"] = "token",
            ["Telegram:ChatId"] = "123"
        };
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        services.AddInfrastructure(configuration);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        using IServiceScope serviceScope = serviceProvider.CreateScope();
        FinanceBotDbContext? financeBotDbContext = serviceScope.ServiceProvider.GetService<
            FinanceBotDbContext>();

        Assert.NotNull(financeBotDbContext);
    }
}
