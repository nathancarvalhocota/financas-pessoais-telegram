using FinanceBot.Api.Telegram;
using FinanceBot.Application.Commands;
using FinanceBot.Application.Commands.Handlers;
using FinanceBot.Infrastructure.DependencyInjection;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<RegisterExpenseCommandHandler>();
builder.Services.AddScoped<ListExpensesCommandHandler>();
builder.Services.AddScoped<DeleteExpenseCommandHandler>();
builder.Services.AddScoped<CategoryLimitCommandHandler>();
builder.Services.AddScoped<TelegramCommandRouter>(serviceProvider => new TelegramCommandRouter(
    serviceProvider.GetRequiredService<RegisterExpenseCommandHandler>(),
    serviceProvider.GetRequiredService<ListExpensesCommandHandler>(),
    serviceProvider.GetRequiredService<DeleteExpenseCommandHandler>(),
    serviceProvider.GetRequiredService<CategoryLimitCommandHandler>()));
builder.Services.Configure<TelegramOptions>(
    builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient<TelegramMessageSender>();
builder.Services.AddInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    FinanceBotDbContext db = scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
}

app.MapGet("/health", static () => Results.Ok("ok"));
app.MapTelegramWebhookEndpoint();

app.Run();

public partial class Program;
