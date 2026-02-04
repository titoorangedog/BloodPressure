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
}
