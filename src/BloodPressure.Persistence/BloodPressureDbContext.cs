using System.Text.Json;
using BloodPressure.Persistence.Entities;
using BloodPressure.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BloodPressure.Persistence;

public sealed class BloodPressureDbContext(DbContextOptions<BloodPressureDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserSettingsEntity> UserSettings => Set<UserSettingsEntity>();
    public DbSet<LicenseEntity> Licenses => Set<LicenseEntity>();
    public DbSet<ReadingEntity> Readings => Set<ReadingEntity>();
    public DbSet<SymptomOptionEntity> SymptomOptions => Set<SymptomOptionEntity>();
    public DbSet<TimeSlotOptionEntity> TimeSlotOptions => Set<TimeSlotOptionEntity>();
    public DbSet<SportActivityOptionEntity> SportActivityOptions => Set<SportActivityOptionEntity>();
    public DbSet<ReadingSymptomEntity> ReadingSymptoms => Set<ReadingSymptomEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dateOnlyConverter = new ValueConverter<DateOnly, DateTime>(
            dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
            dateTime => DateOnly.FromDateTime(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)));

        var nullableDateOnlyConverter = new ValueConverter<DateOnly?, DateTime?>(
            dateOnly => dateOnly.HasValue ? dateOnly.Value.ToDateTime(TimeOnly.MinValue) : null,
            dateTime => dateTime.HasValue ? DateOnly.FromDateTime(DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Utc)) : null);

        var listConverter = new ValueConverter<IReadOnlyCollection<int>, string>(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<IReadOnlyCollection<int>>(value, (JsonSerializerOptions?)null)
                     ?? Array.Empty<int>());

        var listComparer = new ValueComparer<IReadOnlyCollection<int>>(
            (left, right) => (left ?? Array.Empty<int>()).SequenceEqual(right ?? Array.Empty<int>()),
            value => (value ?? Array.Empty<int>()).Aggregate(0, (current, element) => HashCode.Combine(current, element)),
            value => (value ?? Array.Empty<int>()).ToArray());

        var timeSlotDefinitionConverter = new ValueConverter<IReadOnlyCollection<TimeSlotDefinitionEntity>, string>(
            value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
            value => JsonSerializer.Deserialize<IReadOnlyCollection<TimeSlotDefinitionEntity>>(value, (JsonSerializerOptions?)null)
                     ?? Array.Empty<TimeSlotDefinitionEntity>());

        var timeSlotDefinitionComparer = new ValueComparer<IReadOnlyCollection<TimeSlotDefinitionEntity>>(
            (left, right) => JsonSerializer.Serialize(left ?? Array.Empty<TimeSlotDefinitionEntity>(), (JsonSerializerOptions?)null)
                == JsonSerializer.Serialize(right ?? Array.Empty<TimeSlotDefinitionEntity>(), (JsonSerializerOptions?)null),
            value => JsonSerializer.Serialize(value ?? Array.Empty<TimeSlotDefinitionEntity>(), (JsonSerializerOptions?)null).GetHashCode(),
            value => value == null ? Array.Empty<TimeSlotDefinitionEntity>() : value.ToArray());

        modelBuilder.Entity<UserEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.HasIndex(x => x.Email).IsUnique();
            builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
            builder.Property(x => x.Role).HasConversion<int>();
            builder.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<UserSettingsEntity>(builder =>
        {
            builder.HasKey(x => x.UserId);
            builder.Property(x => x.DateOfBirth).HasConversion(nullableDateOnlyConverter);
            builder.Property(x => x.HeightCm).HasPrecision(5, 2);
            builder.HasOne(x => x.User)
                .WithOne(x => x.Settings)
                .HasForeignKey<UserSettingsEntity>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.OwnsOne(x => x.Thresholds, thresholds =>
            {
                thresholds.OwnsOne(t => t.Systolic, set =>
                {
                    set.Property(p => p.VeryLowMax).HasColumnName("SystolicVeryLowMax");
                    set.Property(p => p.LowMax).HasColumnName("SystolicLowMax");
                    set.Property(p => p.NormalMax).HasColumnName("SystolicNormalMax");
                    set.Property(p => p.HighMax).HasColumnName("SystolicHighMax");
                });
                thresholds.OwnsOne(t => t.Diastolic, set =>
                {
                    set.Property(p => p.VeryLowMax).HasColumnName("DiastolicVeryLowMax");
                    set.Property(p => p.LowMax).HasColumnName("DiastolicLowMax");
                    set.Property(p => p.NormalMax).HasColumnName("DiastolicNormalMax");
                    set.Property(p => p.HighMax).HasColumnName("DiastolicHighMax");
                });
            });

            builder.OwnsOne(x => x.DashboardPreferences, prefs =>
            {
                prefs.Property(p => p.DefaultRangeDays).HasDefaultValue(30);
            });

            builder.OwnsOne(x => x.DefaultSelections, defaults =>
            {
                defaults.Property(p => p.SymptomOptionIds)
                    .HasConversion(listConverter)
                    .Metadata.SetValueComparer(listComparer);
                defaults.Property(p => p.SymptomOptionIds).HasColumnName("DefaultSymptomOptionIds");
            });

            builder.OwnsOne(x => x.UiPreferences, ui =>
            {
                ui.Property(p => p.CompactMode).HasDefaultValue(false);
            });

        builder.Property(x => x.TimeSlotDefinitions)
            .HasConversion(timeSlotDefinitionConverter)
            .Metadata.SetValueComparer(timeSlotDefinitionComparer);
    });

        modelBuilder.Entity<LicenseEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Type).HasConversion<int>();
            builder.HasOne(x => x.User)
                .WithMany(x => x.Licenses)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => new { x.UserId, x.IsActive });
        });

        modelBuilder.Entity<ReadingEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Position).HasConversion<int>();
            builder.Property(x => x.Severity).HasConversion<int>();
            builder.Property(x => x.ColorKey).HasConversion<int>();
            builder.Property(x => x.TimestampUtc).HasColumnType("datetimeoffset");
            builder.Property(x => x.WeightKg).HasPrecision(6, 2);
            builder.HasOne(x => x.User)
                .WithMany(x => x.Readings)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SymptomOptionEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<TimeSlotOptionEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<SportActivityOptionEntity>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<ReadingSymptomEntity>(builder =>
        {
            builder.HasKey(x => new { x.ReadingId, x.SymptomOptionId });
            builder.HasOne(x => x.Reading)
                .WithMany(x => x.Symptoms)
                .HasForeignKey(x => x.ReadingId);
            builder.HasOne(x => x.SymptomOption)
                .WithMany(x => x.Readings)
                .HasForeignKey(x => x.SymptomOptionId);
        });

        SeedOptions(modelBuilder);
    }

    private static void SeedOptions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SymptomOptionEntity>().HasData(
            new SymptomOptionEntity { Id = 1, Name = "Headache" },
            new SymptomOptionEntity { Id = 2, Name = "Dizziness" },
            new SymptomOptionEntity { Id = 3, Name = "Nausea" },
            new SymptomOptionEntity { Id = 4, Name = "Chest Pain" },
            new SymptomOptionEntity { Id = 5, Name = "Blurred Vision" });

        modelBuilder.Entity<TimeSlotOptionEntity>().HasData(
            new TimeSlotOptionEntity { Id = 1, Name = "Mattino" },
            new TimeSlotOptionEntity { Id = 2, Name = "Pomeriggio" },
            new TimeSlotOptionEntity { Id = 3, Name = "Sera" },
            new TimeSlotOptionEntity { Id = 4, Name = "Notte" },
            new TimeSlotOptionEntity { Id = 5, Name = "Mezzo giorno" });

        modelBuilder.Entity<SportActivityOptionEntity>().HasData(
            new SportActivityOptionEntity { Id = 1, Name = "None" },
            new SportActivityOptionEntity { Id = 2, Name = "Light" },
            new SportActivityOptionEntity { Id = 3, Name = "Moderate" },
            new SportActivityOptionEntity { Id = 4, Name = "Intense" });
    }
}
