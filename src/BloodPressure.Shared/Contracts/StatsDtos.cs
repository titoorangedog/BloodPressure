using BloodPressure.Shared.Domain;

namespace BloodPressure.Shared.Contracts;

public sealed record StatsFilterRequest
{
    public required DateOnly FromDate { get; init; }
    public required DateOnly ToDate { get; init; }
}

public sealed record DashboardStatsResponse
{
    public required IReadOnlyCollection<BloodPressurePointDto> BloodPressureTrend { get; init; }
    public required IReadOnlyCollection<MorningEveningBloodPressurePointDto> MorningEveningBloodPressureTrend { get; init; }
    public required IReadOnlyCollection<ChartPointDto> HeartRateTrend { get; init; }
    public required IReadOnlyCollection<ChartPointDto> WeightTrend { get; init; }
    public required IReadOnlyCollection<HistogramBinDto> SeverityHistogram { get; init; }
    public required KpiDto Kpis { get; init; }
}

public sealed record BloodPressurePointDto
{
    public required DateOnly Date { get; init; }
    public required decimal Systolic { get; init; }
    public required decimal Diastolic { get; init; }
}

public sealed record MorningEveningBloodPressurePointDto
{
    public required DateOnly Date { get; init; }
    public decimal? MorningSystolic { get; init; }
    public decimal? MorningDiastolic { get; init; }
    public decimal? EveningSystolic { get; init; }
    public decimal? EveningDiastolic { get; init; }
}

public sealed record ChartPointDto
{
    public required DateOnly Date { get; init; }
    public required decimal Value { get; init; }
}

public sealed record HistogramBinDto
{
    public required Severity Severity { get; init; }
    public required int Count { get; init; }
    public required decimal Percentage { get; init; }
}

public sealed record KpiDto
{
    public required decimal AverageSystolic { get; init; }
    public required decimal AverageDiastolic { get; init; }
    public required decimal MedianSystolic { get; init; }
    public required decimal MedianDiastolic { get; init; }
    public required int MinSystolic { get; init; }
    public required int MaxSystolic { get; init; }
    public required int MinDiastolic { get; init; }
    public required int MaxDiastolic { get; init; }
    public required decimal StdDevSystolic { get; init; }
    public required decimal StdDevDiastolic { get; init; }
}
