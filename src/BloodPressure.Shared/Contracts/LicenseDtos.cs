using BloodPressure.Shared.Domain;

namespace BloodPressure.Shared.Contracts;

public sealed record ActiveLicenseResponse
{
    public required LicenseType Type { get; init; }
    public required DateTimeOffset StartDateUtc { get; init; }
    public required DateTimeOffset EndDateUtc { get; init; }
    public required int DaysRemaining { get; init; }
    public required bool IsExpired { get; init; }
}
