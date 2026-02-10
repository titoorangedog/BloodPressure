using System.Security.Claims;
using System.Text;
using System.Xml.Serialization;
using BloodPressure.Persistence;
using BloodPressure.Persistence.Entities;
using BloodPressure.Shared.Auth;
using BloodPressure.Shared.Contracts;
using BloodPressure.Shared.Domain;
using BloodPressure.Shared.Options;
using ClosedXML.Excel;
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
        options.MapInboundClaims = false;
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

api.MapGet("/readings/export/excel", async (
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var exportItems = await LoadExportItemsAsync(user, db, cancellationToken);
    var content = BuildExcelExport(exportItems);
    var fileName = $"readings-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
    return Results.File(
        content,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName);
});

api.MapGet("/readings/template/excel", () =>
{
    var content = BuildExcelExport(Array.Empty<ReadingExportItem>());
    var fileName = "readings-template.xlsx";
    return Results.File(
        content,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName);
});

api.MapGet("/readings/example/excel", () =>
{
    var content = BuildExcelExport(BuildExampleItems());
    var fileName = "readings-example.xlsx";
    return Results.File(
        content,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName);
});

api.MapGet("/readings/export/xml", async (
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var exportItems = await LoadExportItemsAsync(user, db, cancellationToken);
    var exportFile = new ReadingsExportFile { Readings = exportItems };
    var serializer = new XmlSerializer(typeof(ReadingsExportFile));
    using var stream = new MemoryStream();
    serializer.Serialize(stream, exportFile);
    var fileName = $"readings-{DateTime.UtcNow:yyyyMMddHHmmss}.xml";
    return Results.File(
        stream.ToArray(),
        "application/xml",
        fileName);
});

api.MapGet("/readings/template/xml", () =>
{
    var exportFile = new ReadingsExportFile();
    var serializer = new XmlSerializer(typeof(ReadingsExportFile));
    using var stream = new MemoryStream();
    serializer.Serialize(stream, exportFile);
    var fileName = "readings-template.xml";
    return Results.File(
        stream.ToArray(),
        "application/xml",
        fileName);
});

api.MapGet("/readings/example/xml", () =>
{
    var exportFile = new ReadingsExportFile { Readings = BuildExampleItems() };
    var serializer = new XmlSerializer(typeof(ReadingsExportFile));
    using var stream = new MemoryStream();
    serializer.Serialize(stream, exportFile);
    var fileName = "readings-example.xml";
    return Results.File(
        stream.ToArray(),
        "application/xml",
        fileName);
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
    settings.TimeSlotDefinitions = request.TimeSlotDefinitions.Count == 0
        ? BuildDefaultTimeSlotDefinitions()
        : request.TimeSlotDefinitions
            .Select(def => new TimeSlotDefinitionEntity
            {
                Key = def.Key,
                Label = def.Label,
                Start = def.Start,
                End = def.End
            })
            .ToList();

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
    var daysRemaining = LicenseCalculator.CalculateDaysRemaining(license.Type, now, license.EndDateUtc);
    return Results.Ok(new ActiveLicenseResponse
    {
        Type = license.Type,
        StartDateUtc = license.StartDateUtc,
        EndDateUtc = license.EndDateUtc,
        DaysRemaining = daysRemaining,
        IsExpired = LicenseCalculator.IsExpired(license.Type, now, license.EndDateUtc)
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
    var userExists = await db.Users.AnyAsync(x => x.Id == id, cancellationToken);
    if (!userExists)
    {
        return Results.NotFound();
    }

    await db.Licenses
        .Where(x => x.UserId == id && x.IsActive)
        .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsActive, false), cancellationToken);

    var newLicense = new LicenseEntity
    {
        Id = Guid.NewGuid(),
        UserId = id,
        Type = request.Type,
        StartDateUtc = request.StartDateUtc,
        EndDateUtc = request.EndDateUtc,
        IsActive = true
    };

    db.Licenses.Add(newLicense);
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

admin.MapPut("/users/{id:guid}/licenses/{licenseId:guid}", async (
    Guid id,
    Guid licenseId,
    LicenseUpdateRequest request,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var license = await db.Licenses.SingleOrDefaultAsync(
        x => x.UserId == id && x.Id == licenseId, cancellationToken);
    if (license is null)
    {
        return Results.NotFound();
    }

    if (!license.IsActive)
    {
        return Results.BadRequest("Only the active license can be modified.");
    }

    license.Type = request.Type;
    license.StartDateUtc = request.StartDateUtc;
    license.EndDateUtc = request.EndDateUtc;

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(new LicenseDto
    {
        Id = license.Id,
        Type = license.Type,
        StartDateUtc = license.StartDateUtc,
        EndDateUtc = license.EndDateUtc,
        IsActive = license.IsActive
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

static async Task<List<ReadingExportItem>> LoadExportItemsAsync(
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken)
{
    var userId = user.GetUserId();
    var entities = await db.Readings
        .AsNoTracking()
        .Include(x => x.Symptoms)
        .ThenInclude(x => x.SymptomOption)
        .Include(x => x.TimeSlotOption)
        .Include(x => x.SportActivityOption)
        .Where(x => x.UserId == userId)
        .OrderByDescending(x => x.TimestampUtc)
        .ToListAsync(cancellationToken);

    return entities.Select(entity => new ReadingExportItem
        {
            Id = entity.Id,
            Systolic = entity.Systolic,
            Diastolic = entity.Diastolic,
            HeartRate = entity.HeartRate,
            WeightKg = entity.WeightKg,
            TimestampUtc = entity.TimestampUtc,
            DateUtc = entity.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd"),
            TimeUtc = entity.TimestampUtc.UtcDateTime.ToString("HH:mm"),
            Notes = entity.Notes,
            Position = entity.Position,
            MedicationSkipped = entity.MedicationSkipped,
            Severity = entity.Severity,
            ColorKey = entity.ColorKey,
            TimeSlotOptionId = entity.TimeSlotOptionId,
            TimeSlotName = entity.TimeSlotOption?.Name,
            SportActivityOptionId = entity.SportActivityOptionId,
            SportActivityName = entity.SportActivityOption?.Name,
            SymptomOptionIds = entity.Symptoms
                .Where(x => x.SymptomOption is not null)
                .Select(x => x.SymptomOption!.Id)
                .ToList(),
            SymptomOptionNames = entity.Symptoms
                .Where(x => x.SymptomOption is not null)
                .Select(x => x.SymptomOption!.Name)
                .ToList()
        })
        .ToList();
}

static byte[] BuildExcelExport(IReadOnlyCollection<ReadingExportItem> items)
{
    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add("Readings");

    for (var i = 0; i < ReadingImportExportColumns.All.Length; i++)
    {
        sheet.Cell(1, i + 1).Value = ReadingImportExportColumns.All[i];
    }

    var row = 2;
    foreach (var item in items)
    {
        var col = 1;
        sheet.Cell(row, col++).Value = item.Id?.ToString();
        sheet.Cell(row, col++).Value = item.TimestampUtc.ToString("O");
        sheet.Cell(row, col++).Value = item.DateUtc ?? item.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd");
        sheet.Cell(row, col++).Value = item.TimeUtc ?? item.TimestampUtc.UtcDateTime.ToString("HH:mm");
        sheet.Cell(row, col++).Value = item.Systolic;
        sheet.Cell(row, col++).Value = item.Diastolic;
        sheet.Cell(row, col++).Value = item.HeartRate;
        sheet.Cell(row, col++).Value = item.WeightKg;
        sheet.Cell(row, col++).Value = item.Notes;
        sheet.Cell(row, col++).Value = item.Position.ToString();
        sheet.Cell(row, col++).Value = item.MedicationSkipped;
        sheet.Cell(row, col++).Value = item.Severity.ToString();
        sheet.Cell(row, col++).Value = item.ColorKey.ToString();
        sheet.Cell(row, col++).Value = item.TimeSlotOptionId;
        sheet.Cell(row, col++).Value = item.TimeSlotName;
        sheet.Cell(row, col++).Value = item.SportActivityOptionId;
        sheet.Cell(row, col++).Value = item.SportActivityName;
        sheet.Cell(row, col++).Value = string.Join(",", item.SymptomOptionIds);
        sheet.Cell(row, col++).Value = string.Join(",", item.SymptomOptionNames);
        row++;
    }

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    return stream.ToArray();
}

static List<ReadingExportItem> BuildExampleItems()
{
    return
    [
        new ReadingExportItem
        {
            Id = Guid.NewGuid(),
            TimestampUtc = new DateTimeOffset(2026, 1, 15, 7, 30, 0, TimeSpan.Zero),
            Systolic = 120,
            Diastolic = 78,
            HeartRate = 68,
            WeightKg = 72.5m,
            Notes = "Esempio compilato",
            Position = Position.Sitting,
            MedicationSkipped = false,
            Severity = Severity.Normal,
            ColorKey = ColorKey.Green,
            TimeSlotOptionId = null,
            TimeSlotName = "Mattina",
            SportActivityOptionId = null,
            SportActivityName = "Passeggiata",
            SymptomOptionIds = new List<int>(),
            SymptomOptionNames = new List<string> { "Mal di testa" }
        }
    ];
}

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
        UiPreferences = new UiPreferencesEntity { CompactMode = false },
        TimeSlotDefinitions = BuildDefaultTimeSlotDefinitions()
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
        UiPreferences = new UiPreferencesDto { CompactMode = settings.UiPreferences.CompactMode },
        TimeSlotDefinitions = settings.TimeSlotDefinitions
            .Select(def => new TimeSlotDefinitionDto
            {
                Key = def.Key,
                Label = def.Label,
                Start = def.Start,
                End = def.End
            })
            .ToList()
    };
}

static IReadOnlyCollection<TimeSlotDefinitionEntity> BuildDefaultTimeSlotDefinitions()
{
    return new[]
    {
        new TimeSlotDefinitionEntity { Key = "Morning", Label = "Mattino", Start = "06:00", End = "10:59" },
        new TimeSlotDefinitionEntity { Key = "Midday", Label = "Mezzo giorno", Start = "11:00", End = "14:59" },
        new TimeSlotDefinitionEntity { Key = "Afternoon", Label = "Pomeriggio", Start = "15:00", End = "19:59" },
        new TimeSlotDefinitionEntity { Key = "Evening", Label = "Sera", Start = "20:00", End = "23:59" },
        new TimeSlotDefinitionEntity { Key = "Night", Label = "Notte", Start = "00:00", End = "05:59" }
    };
}
