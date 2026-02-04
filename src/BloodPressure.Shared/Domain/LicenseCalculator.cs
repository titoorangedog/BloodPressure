namespace BloodPressure.Shared.Domain;

public static class LicenseCalculator
{
    public static int CalculateDaysRemaining(DateTimeOffset nowUtc, DateTimeOffset endUtc)
    {
        var days = (int)Math.Floor((endUtc - nowUtc).TotalDays);
        return Math.Max(days, 0);
    }
}
