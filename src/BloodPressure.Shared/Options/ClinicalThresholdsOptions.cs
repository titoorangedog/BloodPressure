namespace BloodPressure.Shared.Options;

public sealed record ThresholdSet
{
    public required int VeryLowMax { get; init; }
    public required int LowMax { get; init; }
    public required int NormalLowMax { get; init; }
    public required int NormalOptimalMax { get; init; }
    public required int WarningHighMax { get; init; }
    public required int VeryHighMin { get; init; }
}

public sealed record ClinicalThresholdsOptions
{
    public const string SectionName = "ClinicalThresholds";

    public required ThresholdSet Systolic { get; init; }
    public required ThresholdSet Diastolic { get; init; }
}
