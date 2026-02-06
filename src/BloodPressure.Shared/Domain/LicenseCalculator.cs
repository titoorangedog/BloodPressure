namespace BloodPressure.Shared.Domain;

public static class LicenseCalculator
{
    public const int UnlimitedDays = int.MaxValue;

    public static int CalculateDaysRemaining(DateTimeOffset nowUtc, DateTimeOffset endUtc)
    {
        var days = (int)Math.Floor((endUtc - nowUtc).TotalDays);
        return Math.Max(days, 0);
    }

    public static int CalculateDaysRemaining(LicenseType type, DateTimeOffset nowUtc, DateTimeOffset endUtc)
    {
        return type == LicenseType.Premium ? UnlimitedDays : CalculateDaysRemaining(nowUtc, endUtc);
    }

    public static bool IsExpired(LicenseType type, DateTimeOffset nowUtc, DateTimeOffset endUtc)
    {
        return type != LicenseType.Premium && endUtc <= nowUtc;
    }

    public static (int Years, int Months, int Days) CalculateRemainingParts(DateTimeOffset nowUtc, DateTimeOffset endUtc)
    {
        var start = nowUtc.UtcDateTime.Date;
        var end = endUtc.UtcDateTime.Date;

        if (end <= start)
        {
            return (0, 0, 0);
        }

        var years = 0;
        var cursor = start;
        while (cursor.AddYears(1) <= end)
        {
            years++;
            cursor = cursor.AddYears(1);
        }

        var months = 0;
        while (cursor.AddMonths(1) <= end)
        {
            months++;
            cursor = cursor.AddMonths(1);
        }

        var days = (end - cursor).Days;
        return (years, months, days);
    }

    public static string FormatRemainingItalian(DateTimeOffset nowUtc, DateTimeOffset endUtc)
    {
        var (years, months, days) = CalculateRemainingParts(nowUtc, endUtc);
        var parts = new List<string>(3);

        if (years > 0)
        {
            parts.Add($"{years} {(years == 1 ? "anno" : "anni")}");
        }

        if (months > 0)
        {
            parts.Add($"{months} {(months == 1 ? "mese" : "mesi")}");
        }

        if (days > 0 || parts.Count == 0)
        {
            parts.Add($"{days} {(days == 1 ? "giorno" : "giorni")}");
        }

        return string.Join(" ", parts);
    }
}
