namespace BloodPressure.Persistence.Entities;

public sealed class UserSettingsEntity
{
    public Guid UserId { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public decimal? HeightCm { get; set; }

    public ClinicalThresholdsEntity Thresholds { get; set; } = new();
    public DashboardPreferencesEntity DashboardPreferences { get; set; } = new();
    public DefaultSelectionsEntity DefaultSelections { get; set; } = new();
    public UiPreferencesEntity UiPreferences { get; set; } = new();
    public IReadOnlyCollection<TimeSlotDefinitionEntity> TimeSlotDefinitions { get; set; } = Array.Empty<TimeSlotDefinitionEntity>();

    public UserEntity? User { get; set; }
}

public sealed class ClinicalThresholdsEntity
{
    public ThresholdSetEntity Systolic { get; set; } = new();
    public ThresholdSetEntity Diastolic { get; set; } = new();
}

public sealed class ThresholdSetEntity
{
    public int VeryLowMax { get; set; }
    public int LowMax { get; set; }
    public int NormalLowMax { get; set; }
    public int NormalOptimalMax { get; set; }
    public int WarningHighMax { get; set; }
    public int VeryHighMin { get; set; }
}

public sealed class DashboardPreferencesEntity
{
    public int DefaultRangeDays { get; set; }
}

public sealed class DefaultSelectionsEntity
{
    public IReadOnlyCollection<int> SymptomOptionIds { get; set; } = Array.Empty<int>();
    public int? TimeSlotOptionId { get; set; }
    public int? SportActivityOptionId { get; set; }
}

public sealed class UiPreferencesEntity
{
    public bool CompactMode { get; set; }
}

public sealed class TimeSlotDefinitionEntity
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Start { get; set; } = "00:00";
    public string End { get; set; } = "00:00";
}
