using System.Xml.Serialization;
using BloodPressure.Shared.Domain;

namespace BloodPressure.Shared.Contracts;

public sealed record ReadingExportItem
{
    public Guid? Id { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public int Systolic { get; init; }
    public int Diastolic { get; init; }
    public int? HeartRate { get; init; }
    public decimal? WeightKg { get; init; }
    public string? Notes { get; init; }
    public Position Position { get; init; }
    public bool MedicationSkipped { get; init; }
    public Severity Severity { get; init; }
    public ColorKey ColorKey { get; init; }
    public int? TimeSlotOptionId { get; init; }
    public string? TimeSlotName { get; init; }
    public int? SportActivityOptionId { get; init; }
    public string? SportActivityName { get; init; }
    public List<int> SymptomOptionIds { get; init; } = new();
    public List<string> SymptomOptionNames { get; init; } = new();
}

[XmlRoot("ReadingsExport")]
public sealed class ReadingsExportFile
{
    [XmlElement("Reading")]
    public List<ReadingExportItem> Readings { get; set; } = new();
}

public static class ReadingImportExportColumns
{
    public const string Id = "Id";
    public const string TimestampUtc = "TimestampUtc";
    public const string Systolic = "Systolic";
    public const string Diastolic = "Diastolic";
    public const string HeartRate = "HeartRate";
    public const string WeightKg = "WeightKg";
    public const string Notes = "Notes";
    public const string Position = "Position";
    public const string MedicationSkipped = "MedicationSkipped";
    public const string Severity = "Severity";
    public const string ColorKey = "ColorKey";
    public const string TimeSlotOptionId = "TimeSlotOptionId";
    public const string TimeSlotName = "TimeSlotName";
    public const string SportActivityOptionId = "SportActivityOptionId";
    public const string SportActivityName = "SportActivityName";
    public const string SymptomOptionIds = "SymptomOptionIds";
    public const string SymptomOptionNames = "SymptomOptionNames";

    public static readonly string[] All =
    [
        Id,
        TimestampUtc,
        Systolic,
        Diastolic,
        HeartRate,
        WeightKg,
        Notes,
        Position,
        MedicationSkipped,
        Severity,
        ColorKey,
        TimeSlotOptionId,
        TimeSlotName,
        SportActivityOptionId,
        SportActivityName,
        SymptomOptionIds,
        SymptomOptionNames
    ];
}

public sealed record ImportReadingsResponse
{
    public int ImportedCount { get; init; }
    public int SkippedDuplicates { get; init; }
    public int TotalCount { get; init; }
}
