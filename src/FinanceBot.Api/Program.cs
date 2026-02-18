using FinanceBot.Api.Endpoints;
using FinanceBot.Infrastructure.DependencyInjection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true, reloadOnChange: true);
builder.Services.AddInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

app.MapGet("/health", static () => Results.Ok("ok"));
app.MapTelegramWebhookEndpoint();

app.Run();

public partial class Program;
