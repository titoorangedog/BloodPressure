using BloodPressure.Shared.Domain;
using BloodPressure.Shared.Options;
using Xunit;

namespace BloodPressure.Tests;

public sealed class ClinicalClassificationTests
{
    [Fact]
    public void Classify_UsesHighestSeverityAcrossSystolicAndDiastolic()
    {
        var thresholds = new ClinicalThresholdsOptions
        {
            Systolic = new ThresholdSet
            {
                VeryLowMax = 79,
                LowMax = 99,
                NormalLowMax = 109,
                NormalOptimalMax = 120,
                WarningHighMax = 139,
                VeryHighMin = 180
            },
            Diastolic = new ThresholdSet
            {
                VeryLowMax = 49,
                LowMax = 59,
                NormalLowMax = 69,
                NormalOptimalMax = 80,
                WarningHighMax = 89,
                VeryHighMin = 120
            }
        };

        var (severity, color) = ClinicalClassification.Classify(160, 80, thresholds);

        Assert.Equal(Severity.VeryHigh, severity);
        Assert.Equal(ColorKey.Red, color);
    }
}
