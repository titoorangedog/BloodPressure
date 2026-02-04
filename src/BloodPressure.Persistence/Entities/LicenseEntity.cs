using BloodPressure.Shared.Domain;

namespace BloodPressure.Persistence.Entities;

public sealed class LicenseEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public LicenseType Type { get; set; }
    public DateTimeOffset StartDateUtc { get; set; }
    public DateTimeOffset EndDateUtc { get; set; }
    public bool IsActive { get; set; }

    public UserEntity? User { get; set; }
}
