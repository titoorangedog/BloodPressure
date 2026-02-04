namespace BloodPressure.Shared.Options;

public sealed record ThresholdSet
{
    public required int VeryLowMax { get; init; }
    public required int LowMax { get; init; }
    public required int NormalMax { get; init; }
    public required int HighMax { get; init; }
}

public sealed record ClinicalThresholdsOptions
{
    public const string SectionName = "ClinicalThresholds";

    public required ThresholdSet Systolic { get; init; }
    public required ThresholdSet Diastolic { get; init; }
}
