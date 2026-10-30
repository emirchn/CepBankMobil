namespace CepBankApp.Services;

public static class CardBilling
{
    public static DateTime Cutoff(int year, int month, int statementDay) =>
        new(year, month, Math.Min(Math.Clamp(statementDay, 1, 31), DateTime.DaysInMonth(year, month)));

    public static DateTime LastClosedStatement(DateTime today, int statementDay)
    {
        var thisMonth = Cutoff(today.Year, today.Month, statementDay);
        var month = thisMonth <= today.Date ? thisMonth : thisMonth.AddMonths(-1);
        return Cutoff(month.Year, month.Month, statementDay);
    }

    public static DateTime NextStatement(DateTime previous, int statementDay)
    {
        var month = previous.AddMonths(1);
        return Cutoff(month.Year, month.Month, statementDay);
    }

    public static DateTime StatementDate(DateTime transactionDate, int statementDay)
    {
        statementDay = Math.Clamp(statementDay, 1, 31);
        var date = transactionDate.Date;
        var cutoff = new DateTime(date.Year, date.Month, Math.Min(statementDay, DateTime.DaysInMonth(date.Year, date.Month)));
        if (date < cutoff) return cutoff;
        var nextMonth = date.AddMonths(1);
        return new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(statementDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
    }
}
