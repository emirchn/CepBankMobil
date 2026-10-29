namespace CepBankApp.Services;

public static class CardBilling
{
    public static DateTime StatementDate(DateTime transactionDate, int statementDay)
    {
        statementDay = Math.Clamp(statementDay, 1, 31);
        var date = transactionDate.Date;
        var cutoff = new DateTime(date.Year, date.Month, Math.Min(statementDay, DateTime.DaysInMonth(date.Year, date.Month)));
        if (date <= cutoff) return cutoff;
        var nextMonth = date.AddMonths(1);
        return new DateTime(nextMonth.Year, nextMonth.Month, Math.Min(statementDay, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)));
    }
}
