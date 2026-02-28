namespace FinanceBot.Application.Abstractions;

public interface ITelegramCommandRouter
{
    Task<string> RouteAsync(
        string messageText,
        DateTimeOffset messageDateUtc,
        CancellationToken cancellationToken);
}
