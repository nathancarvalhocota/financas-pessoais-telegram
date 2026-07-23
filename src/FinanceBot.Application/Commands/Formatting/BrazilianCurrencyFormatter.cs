using System.Globalization;

namespace FinanceBot.Application.Commands.Formatting;

internal static class BrazilianCurrencyFormatter
{
    private static readonly CultureInfo Culture = new CultureInfo("pt-BR");

    public static string FormatAmount(double value)
    {
        return value.ToString("N2", Culture);
    }

    public static string FormatSignedAmount(double value)
    {
        string signal = value >= 0 ? "+" : "-";
        return $"{signal}R$ {FormatAmount(Math.Abs(value))}";
    }
}
