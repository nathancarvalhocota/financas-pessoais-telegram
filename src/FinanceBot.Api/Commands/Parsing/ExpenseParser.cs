using System.Globalization;
using System.Text.RegularExpressions;
using FinanceBot.Domain.Categories;

namespace FinanceBot.Api.Commands.Parsing;

public static class ExpenseParser
{
    private static readonly Regex CommandRegex = new(
        "^/compra(?:@[A-Za-z0-9_]+)?\\s+([0-9]+(?:[.,][0-9]{1,2})?)\\s*,\\s*(.+?)\\s*,\\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    public static ExpenseParseResult Parse(string text)
    {
        Match match;
        try
        {
            match = CommandRegex.Match(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return new ExpenseParseResult.Failure(new ExpenseParseError.InvalidSyntax());
        }
        if (!match.Success)
            return new ExpenseParseResult.Failure(new ExpenseParseError.InvalidSyntax());

        string description = match.Groups[2].Value.Trim();
        string categoryText = match.Groups[3].Value.Trim();
        if (!double.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out double amount) || !double.IsFinite(amount) || amount <= 0)
            return new ExpenseParseResult.Failure(new ExpenseParseError.InvalidAmount());
        if (string.IsNullOrWhiteSpace(description))
            return new ExpenseParseResult.Failure(new ExpenseParseError.MissingDescription());
        if (!CategoryCatalog.TryParse(categoryText, out Category category))
            return new ExpenseParseResult.Failure(new ExpenseParseError.UnknownCategory(categoryText));

        return new ExpenseParseResult.Success(amount, description, category);
    }
}

public abstract record ExpenseParseResult
{
    private ExpenseParseResult() { }
    public sealed record Success(double Amount, string Description, Category Category) : ExpenseParseResult;
    public sealed record Failure(ExpenseParseError Error) : ExpenseParseResult;
}

public abstract record ExpenseParseError
{
    private ExpenseParseError() { }
    public sealed record InvalidSyntax : ExpenseParseError;
    public sealed record InvalidAmount : ExpenseParseError;
    public sealed record MissingDescription : ExpenseParseError;
    public sealed record UnknownCategory(string Text) : ExpenseParseError;
}
