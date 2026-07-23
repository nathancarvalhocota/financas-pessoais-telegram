namespace FinanceBot.Application.Commands.Parsing;

internal sealed record TelegramCommand(string Name, string Text);

internal static class TelegramCommandParser
{
    public static TelegramCommand Parse(string messageText)
    {
        string normalizedText = messageText.Trim();
        int commandEndIndex = normalizedText.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        string commandToken = commandEndIndex < 0
            ? normalizedText
            : normalizedText[..commandEndIndex];

        int botNameSeparatorIndex = commandToken.IndexOf('@');
        string commandName = botNameSeparatorIndex < 0
            ? commandToken
            : commandToken[..botNameSeparatorIndex];

        return new TelegramCommand(commandName.ToLowerInvariant(), normalizedText);
    }
}
