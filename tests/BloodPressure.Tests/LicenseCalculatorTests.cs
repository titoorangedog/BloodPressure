using BloodPressure.Shared.Domain;
using Xunit;

namespace BloodPressure.Tests;

public sealed class LicenseCalculatorTests
{
    [Fact]
    public void CalculateDaysRemaining_NeverReturnsNegative()
    {
        var now = DateTimeOffset.UtcNow;
        var end = now.AddDays(-2);

        var days = LicenseCalculator.CalculateDaysRemaining(now, end);

        Assert.Equal(0, days);
    }

    [Fact]
    public void PremiumLicense_IsNeverExpired()
    {
        var now = DateTimeOffset.UtcNow;
        var end = now.AddDays(-30);

        var expired = LicenseCalculator.IsExpired(LicenseType.Premium, now, end);

        Assert.False(expired);
    }

    [Fact]
    public void PremiumLicense_ReturnsUnlimitedDays()
    {
        var now = DateTimeOffset.UtcNow;
        var end = now.AddDays(-30);

        var days = LicenseCalculator.CalculateDaysRemaining(LicenseType.Premium, now, end);

        Assert.Equal(LicenseCalculator.UnlimitedDays, days);
    }

    [Fact]
    public void FormatRemainingItalian_UsesYearsMonthsDays()
    {
        var now = new DateTimeOffset(2026, 2, 6, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2027, 4, 9, 0, 0, 0, TimeSpan.Zero);

        var text = LicenseCalculator.FormatRemainingItalian(now, end);

        Assert.Equal("1 anno 2 mesi 3 giorni", text);
    }
}
