using FinanceBot.Api.Commands;
using FinanceBot.Api.Commands.Handlers;
using FinanceBot.Api.Configuration;
using FinanceBot.Api.Endpoints;
using FinanceBot.Api.Messaging;
using FinanceBot.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddTelegramMessaging(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

// Validate before migrations: ValidateOnStart alone runs later, when the host starts.
_ = app.Services.GetRequiredService<IOptions<TelegramOptions>>().Value;

await app.Services.InitializeDatabaseAsync(app.Logger);

app.MapGet("/health", static () => Results.Ok("ok"));
app.MapTelegramWebhookEndpoint();

app.Run();

public partial class Program;
