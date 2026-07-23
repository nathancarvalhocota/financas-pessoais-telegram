using FinanceBot.Domain.Billing;

namespace FinanceBot.Application.Commands;

internal static class DateTimeConverter
{
    private static readonly TimeZoneInfo SaoPauloTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static (DateTime PeriodStartUtc, DateTime PeriodEndUtc) GetUtcPeriod(DateOnly businessDate)
    {
        return GetUtcPeriod(BillingCycle.GetBillingCycle(businessDate));
    }

    public static (DateTime PeriodStartUtc, DateTime PeriodEndUtc) GetUtcPeriod(BillingCycle cycle)
    {
        DateTime start = cycle.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        DateTime end = cycle.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(start, SaoPauloTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(end, SaoPauloTimeZone));
    }

    public static DateOnly GetBusinessDate(DateTime dateUtc)
    {
        DateTimeOffset localDateTime = TimeZoneInfo.ConvertTime(
            new DateTimeOffset(dateUtc, TimeSpan.Zero),
            SaoPauloTimeZone);

        return DateOnly.FromDateTime(localDateTime.DateTime);
    }

    public static DateTime GetUtcDate(DateOnly localDate)
    {
        DateTime localDateTime = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, SaoPauloTimeZone);
    }
}
