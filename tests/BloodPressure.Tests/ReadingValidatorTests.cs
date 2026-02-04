using BloodPressure.Shared.Contracts;
using BloodPressure.Shared.Domain;
using Xunit;

namespace BloodPressure.Tests;

public sealed class ReadingValidatorTests
{
    [Fact]
    public void Validator_RejectsInvalidDiastolic()
    {
        var request = new ReadingCreateRequest
        {
            Systolic = 120,
            Diastolic = 140,
            TimestampUtc = DateTimeOffset.UtcNow,
            Position = Position.Sitting,
            MedicationSkipped = false
        };

        var isValid = ReadingValidator.TryValidate(request, out var error);

        Assert.False(isValid);
        Assert.Contains("Diastolic", error, StringComparison.OrdinalIgnoreCase);
    }
}
