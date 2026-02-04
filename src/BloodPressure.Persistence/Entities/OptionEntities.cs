namespace BloodPressure.Persistence.Entities;

public sealed class SymptomOptionEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public List<ReadingSymptomEntity> Readings { get; set; } = new();
}

public sealed class TimeSlotOptionEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public sealed class SportActivityOptionEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public sealed class ReadingSymptomEntity
{
    public Guid ReadingId { get; set; }
    public int SymptomOptionId { get; set; }

    public ReadingEntity? Reading { get; set; }
    public SymptomOptionEntity? SymptomOption { get; set; }
}
