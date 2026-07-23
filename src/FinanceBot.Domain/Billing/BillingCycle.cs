namespace FinanceBot.Domain.Billing;

public readonly record struct BillingCycle(DateOnly StartDate, DateOnly EndDate)
{
    public static BillingCycle GetBillingCycle(DateOnly date)
    {
        int year = date.Year;
        int month = date.Month;

        if (date.Day < 15)
        {
            month--;
            if (month == 0)
            {
                month = 12;
                year--;
            }
        }

        DateOnly startDate = new DateOnly(year, month, 15);
        return new BillingCycle(startDate, startDate.AddMonths(1).AddDays(-1));
    }
}
