namespace BloodPressure.Shared.Contracts;

public sealed record OptionListResponse
{
    public required IReadOnlyCollection<OptionItemDto> SymptomOptions { get; init; }
    public required IReadOnlyCollection<OptionItemDto> TimeSlotOptions { get; init; }
    public required IReadOnlyCollection<OptionItemDto> SportActivityOptions { get; init; }
}
