using BloodPressure.Shared.Options;

namespace BloodPressure.Shared.Domain;

public static class ClinicalClassification
{
    public static (Severity Severity, ColorKey ColorKey) Classify(
        int systolic,
        int diastolic,
        ClinicalThresholdsOptions thresholds)
    {
        var systolicSeverity = ClassifyValue(systolic, thresholds.Systolic);
        var diastolicSeverity = ClassifyValue(diastolic, thresholds.Diastolic);
        var severity = (Severity)Math.Max((int)systolicSeverity, (int)diastolicSeverity);

        return (severity, severity switch
        {
            Severity.VeryLow => ColorKey.BlueViolet,
            Severity.Low => ColorKey.LightBlue,
            Severity.Normal => ColorKey.Green,
            Severity.High => ColorKey.Orange,
            Severity.VeryHigh => ColorKey.Red,
            _ => ColorKey.Green
        });
    }

    public static Severity ClassifyValue(int value, ThresholdSet thresholds)
    {
        if (value <= thresholds.VeryLowMax) return Severity.VeryLow;
        if (value <= thresholds.LowMax) return Severity.Low;
        if (value <= thresholds.NormalLowMax) return Severity.Normal;
        if (value <= thresholds.NormalOptimalMax) return Severity.Normal;
        if (value <= thresholds.WarningHighMax) return Severity.High;
        if (value >= thresholds.VeryHighMin) return Severity.VeryHigh;
        return Severity.High;
    }
}
