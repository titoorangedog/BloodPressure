using BloodPressure.Shared.Domain;

namespace BloodPressure.Shared.Contracts;

public sealed record LoginUrlResponse
{
    public required string Url { get; init; }
}

public sealed record TokenResponse
{
    public required string AccessToken { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public required UserRole Role { get; init; }
    public required LicenseType LicenseType { get; init; }
}
