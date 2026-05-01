using FinanceBot.Api.Endpoints;
using FinanceBot.Application.Abstractions;
using FinanceBot.Application.UseCases;
using FinanceBot.Infrastructure.DependencyInjection;
using FinanceBot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
builder.Services.AddScoped<ITelegramCommandRouter, TelegramCommandRouter>();
builder.Services.AddInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    FinanceBotDbContext db = scope.ServiceProvider.GetRequiredService<FinanceBotDbContext>();
    await db.Database.MigrateAsync();
}

app.MapGet("/health", static () => Results.Ok("ok"));
app.MapTelegramWebhookEndpoint();

app.Run();

public partial class Program;
