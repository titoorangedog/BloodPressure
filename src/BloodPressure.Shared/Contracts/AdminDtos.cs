using BloodPressure.Shared.Domain;

namespace BloodPressure.Shared.Contracts;

public sealed record UserSummaryDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required UserRole Role { get; init; }
    public required LicenseType ActiveLicense { get; init; }
    public required DateTimeOffset LicenseEndDateUtc { get; init; }
}

public sealed record UserDetailDto
{
    public required Guid Id { get; init; }
    public required string Email { get; init; }
    public required UserRole Role { get; init; }
    public required IReadOnlyCollection<LicenseDto> Licenses { get; init; }
}

public sealed record AssignRoleRequest
{
    public required UserRole Role { get; init; }
}

public sealed record AssignLicenseRequest
{
    public required LicenseType Type { get; init; }
    public required DateTimeOffset StartDateUtc { get; init; }
    public required DateTimeOffset EndDateUtc { get; init; }
}

public sealed record LicenseDto
{
    public required Guid Id { get; init; }
    public required LicenseType Type { get; init; }
    public required DateTimeOffset StartDateUtc { get; init; }
    public required DateTimeOffset EndDateUtc { get; init; }
    public required bool IsActive { get; init; }
}
