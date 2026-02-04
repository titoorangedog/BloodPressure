using BloodPressure.Shared.Domain;

namespace BloodPressure.Persistence.Entities;

public sealed class ReadingEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public int Systolic { get; set; }
    public int Diastolic { get; set; }
    public int? HeartRate { get; set; }
    public decimal? WeightKg { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public string? Notes { get; set; }
    public Position Position { get; set; }
    public bool MedicationSkipped { get; set; }

    public Severity Severity { get; set; }
    public ColorKey ColorKey { get; set; }

    public int? TimeSlotOptionId { get; set; }
    public TimeSlotOptionEntity? TimeSlotOption { get; set; }

    public int? SportActivityOptionId { get; set; }
    public SportActivityOptionEntity? SportActivityOption { get; set; }

    public UserEntity? User { get; set; }
    public List<ReadingSymptomEntity> Symptoms { get; set; } = new();
}
