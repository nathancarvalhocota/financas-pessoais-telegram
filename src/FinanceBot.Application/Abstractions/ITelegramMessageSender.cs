namespace FinanceBot.Application.Abstractions;

public interface ITelegramMessageSender
{
    Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken);
}
