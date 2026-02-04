using BloodPressure.Shared.Domain;

namespace BloodPressure.Persistence.Entities;

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public UserSettingsEntity? Settings { get; set; }
    public List<LicenseEntity> Licenses { get; set; } = new();
    public List<ReadingEntity> Readings { get; set; } = new();
}
