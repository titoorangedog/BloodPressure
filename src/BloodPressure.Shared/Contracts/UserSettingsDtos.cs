using BloodPressure.Shared.Domain;

namespace BloodPressure.Shared.Contracts;

public sealed record UserSettingsResponse
{
    public required Guid UserId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public decimal? HeightCm { get; init; }

    public required ClinicalThresholdsDto Thresholds { get; init; }

    public required DashboardPreferencesDto DashboardPreferences { get; init; }
    public required DefaultSelectionsDto DefaultSelections { get; init; }
    public required UiPreferencesDto UiPreferences { get; init; }
    public IReadOnlyCollection<TimeSlotDefinitionDto> TimeSlotDefinitions { get; init; } = Array.Empty<TimeSlotDefinitionDto>();
}

public sealed record UserSettingsUpdateRequest
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public decimal? HeightCm { get; init; }

    public required ClinicalThresholdsDto Thresholds { get; init; }

    public required DashboardPreferencesDto DashboardPreferences { get; init; }
    public required DefaultSelectionsDto DefaultSelections { get; init; }
    public required UiPreferencesDto UiPreferences { get; init; }
    public IReadOnlyCollection<TimeSlotDefinitionDto> TimeSlotDefinitions { get; init; } = Array.Empty<TimeSlotDefinitionDto>();
}

public sealed record ClinicalThresholdsDto
{
    public required ThresholdSetDto Systolic { get; init; }
    public required ThresholdSetDto Diastolic { get; init; }
}

public sealed record ThresholdSetDto
{
    public required int VeryLowMax { get; init; }
    public required int LowMax { get; init; }
    public required int NormalMax { get; init; }
    public required int HighMax { get; init; }
}

public sealed record DashboardPreferencesDto
{
    public required int DefaultRangeDays { get; init; }
}

public sealed record DefaultSelectionsDto
{
    public IReadOnlyCollection<int> SymptomOptionIds { get; init; } = Array.Empty<int>();
    public int? TimeSlotOptionId { get; init; }
    public int? SportActivityOptionId { get; init; }
}

public sealed record UiPreferencesDto
{
    public required bool CompactMode { get; init; }
}

public sealed record TimeSlotDefinitionDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Start { get; init; }
    public required string End { get; init; }
}
