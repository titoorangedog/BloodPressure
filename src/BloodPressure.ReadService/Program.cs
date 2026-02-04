using System.Security.Claims;
using System.Text;
using BloodPressure.Persistence;
using BloodPressure.Persistence.Entities;
using BloodPressure.Shared.Auth;
using BloodPressure.Shared.Contracts;
using BloodPressure.Shared.Domain;
using BloodPressure.Shared.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey), "Jwt:SigningKey is required.")
    .ValidateOnStart();

builder.Services.AddOptions<ClinicalThresholdsOptions>()
    .BindConfiguration(ClinicalThresholdsOptions.SectionName)
    .ValidateOnStart();

builder.Services.AddDbContext<BloodPressureDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            RoleClaimType = AuthConstants.RoleClaim,
            NameClaimType = ClaimTypes.NameIdentifier,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRole.Admin.ToString()));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty);

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.MapOpenApi();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

var api = app.MapGroup("/")
    .RequireAuthorization();

api.MapGet("/readings", async (
    DateTimeOffset? fromUtc,
    DateTimeOffset? toUtc,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = user.GetUserId();
    var query = db.Readings
        .AsNoTracking()
        .Include(x => x.Symptoms)
        .ThenInclude(x => x.SymptomOption)
        .Include(x => x.TimeSlotOption)
        .Include(x => x.SportActivityOption)
        .Where(x => x.UserId == userId);

    if (fromUtc.HasValue)
    {
        query = query.Where(x => x.TimestampUtc >= fromUtc.Value);
    }

    if (toUtc.HasValue)
    {
        query = query.Where(x => x.TimestampUtc <= toUtc.Value);
    }

    var results = await query
        .OrderByDescending(x => x.TimestampUtc)
        .Select(entity => new ReadingResponse
        {
            Id = entity.Id,
            Systolic = entity.Systolic,
            Diastolic = entity.Diastolic,
            HeartRate = entity.HeartRate,
            WeightKg = entity.WeightKg,
            TimestampUtc = entity.TimestampUtc,
            Notes = entity.Notes,
            Position = entity.Position,
            MedicationSkipped = entity.MedicationSkipped,
            Severity = entity.Severity,
            ColorKey = entity.ColorKey,
            Symptoms = entity.Symptoms
                .Where(x => x.SymptomOption != null)
                .Select(x => new OptionItemDto { Id = x.SymptomOption!.Id, Name = x.SymptomOption!.Name })
                .ToList(),
            TimeSlot = entity.TimeSlotOption == null
                ? null
                : new OptionItemDto { Id = entity.TimeSlotOption.Id, Name = entity.TimeSlotOption.Name },
            SportActivity = entity.SportActivityOption == null
                ? null
                : new OptionItemDto { Id = entity.SportActivityOption.Id, Name = entity.SportActivityOption.Name }
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(results);
});

api.MapGet("/readings/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = user.GetUserId();
    var entity = await db.Readings
        .AsNoTracking()
        .Include(x => x.Symptoms)
        .ThenInclude(x => x.SymptomOption)
        .Include(x => x.TimeSlotOption)
        .Include(x => x.SportActivityOption)
        .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    if (entity is null)
    {
        return Results.NotFound();
    }

    var response = new ReadingResponse
    {
        Id = entity.Id,
        Systolic = entity.Systolic,
        Diastolic = entity.Diastolic,
        HeartRate = entity.HeartRate,
        WeightKg = entity.WeightKg,
        TimestampUtc = entity.TimestampUtc,
        Notes = entity.Notes,
        Position = entity.Position,
        MedicationSkipped = entity.MedicationSkipped,
        Severity = entity.Severity,
        ColorKey = entity.ColorKey,
        Symptoms = entity.Symptoms
            .Where(x => x.SymptomOption != null)
            .Select(x => new OptionItemDto { Id = x.SymptomOption!.Id, Name = x.SymptomOption!.Name })
            .ToList(),
        TimeSlot = entity.TimeSlotOption == null
            ? null
            : new OptionItemDto { Id = entity.TimeSlotOption.Id, Name = entity.TimeSlotOption.Name },
        SportActivity = entity.SportActivityOption == null
            ? null
            : new OptionItemDto { Id = entity.SportActivityOption.Id, Name = entity.SportActivityOption.Name }
    };

    return Results.Ok(response);
});

api.MapGet("/options", async (BloodPressureDbContext db, CancellationToken cancellationToken) =>
{
    var symptomOptions = await db.SymptomOptions
        .AsNoTracking()
        .OrderBy(x => x.Id)
        .Select(x => new OptionItemDto { Id = x.Id, Name = x.Name })
        .ToListAsync(cancellationToken);

    var timeSlots = await db.TimeSlotOptions
        .AsNoTracking()
        .OrderBy(x => x.Id)
        .Select(x => new OptionItemDto { Id = x.Id, Name = x.Name })
        .ToListAsync(cancellationToken);

    var sportActivities = await db.SportActivityOptions
        .AsNoTracking()
        .OrderBy(x => x.Id)
        .Select(x => new OptionItemDto { Id = x.Id, Name = x.Name })
        .ToListAsync(cancellationToken);

    return Results.Ok(new OptionListResponse
    {
        SymptomOptions = symptomOptions,
        TimeSlotOptions = timeSlots,
        SportActivityOptions = sportActivities
    });
});

api.MapGet("/settings/me", async (
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    IOptions<ClinicalThresholdsOptions> defaults,
    CancellationToken cancellationToken) =>
{
    var userId = user.GetUserId();
    var settings = await db.UserSettings.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    if (settings is null)
    {
        settings = BuildDefaultSettings(userId, defaults.Value);
        db.UserSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(MapSettings(settings));
});

api.MapPut("/settings/me", async (
    UserSettingsUpdateRequest request,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = user.GetUserId();
    var settings = await db.UserSettings.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    if (settings is null)
    {
        settings = new UserSettingsEntity { UserId = userId };
        db.UserSettings.Add(settings);
    }

    settings.FirstName = request.FirstName;
    settings.LastName = request.LastName;
    settings.Gender = request.Gender;
    settings.DateOfBirth = request.DateOfBirth;
    settings.HeightCm = request.HeightCm;
    settings.Thresholds = new ClinicalThresholdsEntity
    {
        Systolic = new ThresholdSetEntity
        {
            VeryLowMax = request.Thresholds.Systolic.VeryLowMax,
            LowMax = request.Thresholds.Systolic.LowMax,
            NormalMax = request.Thresholds.Systolic.NormalMax,
            HighMax = request.Thresholds.Systolic.HighMax
        },
        Diastolic = new ThresholdSetEntity
        {
            VeryLowMax = request.Thresholds.Diastolic.VeryLowMax,
            LowMax = request.Thresholds.Diastolic.LowMax,
            NormalMax = request.Thresholds.Diastolic.NormalMax,
            HighMax = request.Thresholds.Diastolic.HighMax
        }
    };
    settings.DashboardPreferences = new DashboardPreferencesEntity
    {
        DefaultRangeDays = request.DashboardPreferences.DefaultRangeDays
    };
    settings.DefaultSelections = new DefaultSelectionsEntity
    {
        SymptomOptionIds = request.DefaultSelections.SymptomOptionIds,
        TimeSlotOptionId = request.DefaultSelections.TimeSlotOptionId,
        SportActivityOptionId = request.DefaultSelections.SportActivityOptionId
    };
    settings.UiPreferences = new UiPreferencesEntity
    {
        CompactMode = request.UiPreferences.CompactMode
    };

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(MapSettings(settings));
});

api.MapGet("/licenses/me", async (
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = user.GetUserId();
    var license = await db.Licenses
        .AsNoTracking()
        .Where(x => x.UserId == userId && x.IsActive)
        .OrderByDescending(x => x.EndDateUtc)
        .FirstOrDefaultAsync(cancellationToken);

    if (license is null)
    {
        return Results.NotFound();
    }

    var now = DateTimeOffset.UtcNow;
    var daysRemaining = LicenseCalculator.CalculateDaysRemaining(now, license.EndDateUtc);
    return Results.Ok(new ActiveLicenseResponse
    {
        Type = license.Type,
        StartDateUtc = license.StartDateUtc,
        EndDateUtc = license.EndDateUtc,
        DaysRemaining = daysRemaining,
        IsExpired = license.EndDateUtc <= now
    });
});

var admin = api.MapGroup("/admin")
    .RequireAuthorization("AdminOnly");

admin.MapGet("/users", async (BloodPressureDbContext db, CancellationToken cancellationToken) =>
{
    var users = await db.Users
        .AsNoTracking()
        .Include(x => x.Licenses)
        .OrderBy(x => x.Email)
        .Select(x => new UserSummaryDto
        {
            Id = x.Id,
            Email = x.Email,
            Role = x.Role,
            ActiveLicense = x.Licenses.Where(l => l.IsActive).Select(l => l.Type).FirstOrDefault(),
            LicenseEndDateUtc = x.Licenses.Where(l => l.IsActive).Select(l => l.EndDateUtc).FirstOrDefault()
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(users);
});

admin.MapGet("/users/{id:guid}", async (
    Guid id,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var user = await db.Users
        .AsNoTracking()
        .Include(x => x.Licenses)
        .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    if (user is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new UserDetailDto
    {
        Id = user.Id,
        Email = user.Email,
        Role = user.Role,
        Licenses = user.Licenses
            .OrderByDescending(x => x.StartDateUtc)
            .Select(x => new LicenseDto
            {
                Id = x.Id,
                Type = x.Type,
                StartDateUtc = x.StartDateUtc,
                EndDateUtc = x.EndDateUtc,
                IsActive = x.IsActive
            })
            .ToList()
    });
});

admin.MapPut("/users/{id:guid}/role", async (
    Guid id,
    AssignRoleRequest request,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (user is null)
    {
        return Results.NotFound();
    }

    user.Role = request.Role;
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

admin.MapPost("/users/{id:guid}/licenses", async (
    Guid id,
    AssignLicenseRequest request,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var user = await db.Users.Include(x => x.Licenses).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (user is null)
    {
        return Results.NotFound();
    }

    foreach (var license in user.Licenses.Where(x => x.IsActive))
    {
        license.IsActive = false;
    }

    var newLicense = new LicenseEntity
    {
        Id = Guid.NewGuid(),
        UserId = user.Id,
        Type = request.Type,
        StartDateUtc = request.StartDateUtc,
        EndDateUtc = request.EndDateUtc,
        IsActive = true
    };

    user.Licenses.Add(newLicense);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new LicenseDto
    {
        Id = newLicense.Id,
        Type = newLicense.Type,
        StartDateUtc = newLicense.StartDateUtc,
        EndDateUtc = newLicense.EndDateUtc,
        IsActive = true
    });
});

admin.MapPost("/users/{id:guid}/licenses/{licenseId:guid}/terminate", async (
    Guid id,
    Guid licenseId,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var license = await db.Licenses.SingleOrDefaultAsync(x => x.UserId == id && x.Id == licenseId, cancellationToken);
    if (license is null)
    {
        return Results.NotFound();
    }

    license.IsActive = false;
    license.EndDateUtc = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BloodPressureDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

static UserSettingsEntity BuildDefaultSettings(Guid userId, ClinicalThresholdsOptions options)
{
    return new UserSettingsEntity
    {
        UserId = userId,
        Thresholds = new ClinicalThresholdsEntity
        {
            Systolic = new ThresholdSetEntity
            {
                VeryLowMax = options.Systolic.VeryLowMax,
                LowMax = options.Systolic.LowMax,
                NormalMax = options.Systolic.NormalMax,
                HighMax = options.Systolic.HighMax
            },
            Diastolic = new ThresholdSetEntity
            {
                VeryLowMax = options.Diastolic.VeryLowMax,
                LowMax = options.Diastolic.LowMax,
                NormalMax = options.Diastolic.NormalMax,
                HighMax = options.Diastolic.HighMax
            }
        },
        DashboardPreferences = new DashboardPreferencesEntity { DefaultRangeDays = 30 },
        DefaultSelections = new DefaultSelectionsEntity(),
        UiPreferences = new UiPreferencesEntity { CompactMode = false }
    };
}

static UserSettingsResponse MapSettings(UserSettingsEntity settings)
{
    return new UserSettingsResponse
    {
        UserId = settings.UserId,
        FirstName = settings.FirstName,
        LastName = settings.LastName,
        Gender = settings.Gender,
        DateOfBirth = settings.DateOfBirth,
        HeightCm = settings.HeightCm,
        Thresholds = new ClinicalThresholdsDto
        {
            Systolic = new ThresholdSetDto
            {
                VeryLowMax = settings.Thresholds.Systolic.VeryLowMax,
                LowMax = settings.Thresholds.Systolic.LowMax,
                NormalMax = settings.Thresholds.Systolic.NormalMax,
                HighMax = settings.Thresholds.Systolic.HighMax
            },
            Diastolic = new ThresholdSetDto
            {
                VeryLowMax = settings.Thresholds.Diastolic.VeryLowMax,
                LowMax = settings.Thresholds.Diastolic.LowMax,
                NormalMax = settings.Thresholds.Diastolic.NormalMax,
                HighMax = settings.Thresholds.Diastolic.HighMax
            }
        },
        DashboardPreferences = new DashboardPreferencesDto { DefaultRangeDays = settings.DashboardPreferences.DefaultRangeDays },
        DefaultSelections = new DefaultSelectionsDto
        {
            SymptomOptionIds = settings.DefaultSelections.SymptomOptionIds,
            TimeSlotOptionId = settings.DefaultSelections.TimeSlotOptionId,
            SportActivityOptionId = settings.DefaultSelections.SportActivityOptionId
        },
        UiPreferences = new UiPreferencesDto { CompactMode = settings.UiPreferences.CompactMode }
    };
}
