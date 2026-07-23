namespace FinanceBot.Domain.Billing;

public enum SpendingAlertLevel
{
    Warning,
    Danger,
    Urgent
}

public static class SpendingAlertPolicy
{
    public static SpendingAlertLevel? GetCrossedLevel(
        double previousCycleTotal,
        double totalBeforeExpense,
        double currentCycleTotal)
    {
        if (previousCycleTotal <= 0)
        {
            return null;
        }

        if (totalBeforeExpense < previousCycleTotal * 2 && currentCycleTotal >= previousCycleTotal * 2)
        {
            return SpendingAlertLevel.Urgent;
        }

        if (totalBeforeExpense < previousCycleTotal * 1.5 && currentCycleTotal >= previousCycleTotal * 1.5)
        {
            return SpendingAlertLevel.Danger;
        }

        if (totalBeforeExpense < previousCycleTotal * 1.25 && currentCycleTotal >= previousCycleTotal * 1.25)
        {
            return SpendingAlertLevel.Warning;
        }

        return null;
    }
}
