using BloodPressure.Shared.Domain;

namespace BloodPressure.Shared.Contracts;

public sealed record ReadingCreateRequest
{
    public required int Systolic { get; init; }
    public required int Diastolic { get; init; }
    public int? HeartRate { get; init; }
    public decimal? WeightKg { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public string? Notes { get; init; }
    public required Position Position { get; init; }
    public required bool MedicationSkipped { get; init; }

    public IReadOnlyCollection<int> SymptomOptionIds { get; init; } = Array.Empty<int>();
    public int? TimeSlotOptionId { get; init; }
    public int? SportActivityOptionId { get; init; }
}

public sealed record ReadingUpdateRequest
{
    public required int Systolic { get; init; }
    public required int Diastolic { get; init; }
    public int? HeartRate { get; init; }
    public decimal? WeightKg { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public string? Notes { get; init; }
    public required Position Position { get; init; }
    public required bool MedicationSkipped { get; init; }

    public IReadOnlyCollection<int> SymptomOptionIds { get; init; } = Array.Empty<int>();
    public int? TimeSlotOptionId { get; init; }
    public int? SportActivityOptionId { get; init; }
}

public sealed record ReadingResponse
{
    public required Guid Id { get; init; }
    public required int Systolic { get; init; }
    public required int Diastolic { get; init; }
    public int? HeartRate { get; init; }
    public decimal? WeightKg { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public string? Notes { get; init; }
    public required Position Position { get; init; }
    public required bool MedicationSkipped { get; init; }

    public required Severity Severity { get; init; }
    public required ColorKey ColorKey { get; init; }

    public IReadOnlyCollection<OptionItemDto> Symptoms { get; init; } = Array.Empty<OptionItemDto>();
    public OptionItemDto? TimeSlot { get; init; }
    public OptionItemDto? SportActivity { get; init; }
}

public sealed record OptionItemDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}
