using FinanceBot.Domain.Billing;

namespace FinanceBot.Api.Commands;

internal static class DateTimeConverter
{
    private static readonly TimeZoneInfo SaoPauloTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static (DateTime PeriodStartUtc, DateTime PeriodEndExclusiveUtc) GetUtcPeriod(DateOnly businessDate)
    {
        return GetUtcPeriod(BillingCycle.GetBillingCycle(businessDate));
    }

    public static (DateTime PeriodStartUtc, DateTime PeriodEndExclusiveUtc) GetUtcPeriod(BillingCycle cycle)
    {
        return GetUtcPeriod(cycle.StartDate, cycle.EndDate);
    }

    public static (DateTime PeriodStartUtc, DateTime PeriodEndExclusiveUtc) GetUtcPeriod(
        DateOnly startDate,
        DateOnly endDateInclusive)
    {
        return (
            GetUtcDate(startDate),
            GetUtcDate(endDateInclusive.AddDays(1)));
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
