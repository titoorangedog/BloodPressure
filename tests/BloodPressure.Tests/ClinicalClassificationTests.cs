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
            Systolic = new ThresholdSet { VeryLowMax = 90, LowMax = 100, NormalMax = 130, HighMax = 150 },
            Diastolic = new ThresholdSet { VeryLowMax = 60, LowMax = 70, NormalMax = 85, HighMax = 95 }
        };

        var (severity, color) = ClinicalClassification.Classify(160, 80, thresholds);

        Assert.Equal(Severity.VeryHigh, severity);
        Assert.Equal(ColorKey.Red, color);
    }
}
