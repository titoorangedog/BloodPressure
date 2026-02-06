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
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
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

var readings = app.MapGroup("/readings")
    .RequireAuthorization();

readings.MapPost("/", async (
    ReadingCreateRequest request,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    IOptions<ClinicalThresholdsOptions> thresholds,
    CancellationToken cancellationToken) =>
{
    if (!ReadingValidator.TryValidate(request, out var error))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["Reading"] = [error] });
    }

    var userId = user.GetUserId();
    var effectiveThresholds = await ResolveThresholdsAsync(userId, db, thresholds, cancellationToken);

    var (severity, colorKey) = ClinicalClassification.Classify(request.Systolic, request.Diastolic, effectiveThresholds);

    var entity = new ReadingEntity
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Systolic = request.Systolic,
        Diastolic = request.Diastolic,
        HeartRate = request.HeartRate,
        WeightKg = request.WeightKg,
        TimestampUtc = request.TimestampUtc,
        Notes = request.Notes,
        Position = request.Position,
        MedicationSkipped = request.MedicationSkipped,
        Severity = severity,
        ColorKey = colorKey,
        TimeSlotOptionId = request.TimeSlotOptionId,
        SportActivityOptionId = request.SportActivityOptionId
    };

    await AttachSymptomsAsync(entity, request.SymptomOptionIds, db, cancellationToken);

    db.Readings.Add(entity);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(await BuildResponseAsync(entity.Id, db, cancellationToken));
});

readings.MapPut("/{id:guid}", async (
    Guid id,
    ReadingUpdateRequest request,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    IOptions<ClinicalThresholdsOptions> thresholds,
    CancellationToken cancellationToken) =>
{
    if (!ReadingValidator.TryValidate(new ReadingCreateRequest
        {
            Systolic = request.Systolic,
            Diastolic = request.Diastolic,
            HeartRate = request.HeartRate,
            WeightKg = request.WeightKg,
            TimestampUtc = request.TimestampUtc,
            Notes = request.Notes,
            Position = request.Position,
            MedicationSkipped = request.MedicationSkipped,
            SymptomOptionIds = request.SymptomOptionIds,
            TimeSlotOptionId = request.TimeSlotOptionId,
            SportActivityOptionId = request.SportActivityOptionId
        },
        out var error))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["Reading"] = [error] });
    }

    var userId = user.GetUserId();
    var entity = await db.Readings
        .Include(x => x.Symptoms)
        .SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);

    if (entity is null)
    {
        return Results.NotFound();
    }

    var effectiveThresholds = await ResolveThresholdsAsync(userId, db, thresholds, cancellationToken);
    var (severity, colorKey) = ClinicalClassification.Classify(request.Systolic, request.Diastolic, effectiveThresholds);

    entity.Systolic = request.Systolic;
    entity.Diastolic = request.Diastolic;
    entity.HeartRate = request.HeartRate;
    entity.WeightKg = request.WeightKg;
    entity.TimestampUtc = request.TimestampUtc;
    entity.Notes = request.Notes;
    entity.Position = request.Position;
    entity.MedicationSkipped = request.MedicationSkipped;
    entity.Severity = severity;
    entity.ColorKey = colorKey;
    entity.TimeSlotOptionId = request.TimeSlotOptionId;
    entity.SportActivityOptionId = request.SportActivityOptionId;

    entity.Symptoms.Clear();
    await AttachSymptomsAsync(entity, request.SymptomOptionIds, db, cancellationToken);

    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(await BuildResponseAsync(entity.Id, db, cancellationToken));
});

readings.MapDelete("/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = user.GetUserId();
    var entity = await db.Readings.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    if (entity is null)
    {
        return Results.NotFound();
    }

    db.Readings.Remove(entity);
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

app.Run();


static async Task AttachSymptomsAsync(
    ReadingEntity entity,
    IReadOnlyCollection<int> symptomOptionIds,
    BloodPressureDbContext db,
    CancellationToken cancellationToken)
{
    if (symptomOptionIds.Count == 0)
    {
        return;
    }

    var validIds = await db.SymptomOptions
        .Where(x => symptomOptionIds.Contains(x.Id))
        .Select(x => x.Id)
        .ToListAsync(cancellationToken);

    foreach (var id in validIds)
    {
        entity.Symptoms.Add(new ReadingSymptomEntity { ReadingId = entity.Id, SymptomOptionId = id });
    }
}

static async Task<ClinicalThresholdsOptions> ResolveThresholdsAsync(
    Guid userId,
    BloodPressureDbContext db,
    IOptions<ClinicalThresholdsOptions> defaults,
    CancellationToken cancellationToken)
{
    var settings = await db.UserSettings.SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    if (settings is null)
    {
        return defaults.Value;
    }

    return new ClinicalThresholdsOptions
    {
        Systolic = new ThresholdSet
        {
            VeryLowMax = settings.Thresholds.Systolic.VeryLowMax,
            LowMax = settings.Thresholds.Systolic.LowMax,
            NormalMax = settings.Thresholds.Systolic.NormalMax,
            HighMax = settings.Thresholds.Systolic.HighMax
        },
        Diastolic = new ThresholdSet
        {
            VeryLowMax = settings.Thresholds.Diastolic.VeryLowMax,
            LowMax = settings.Thresholds.Diastolic.LowMax,
            NormalMax = settings.Thresholds.Diastolic.NormalMax,
            HighMax = settings.Thresholds.Diastolic.HighMax
        }
    };
}

static async Task<ReadingResponse> BuildResponseAsync(
    Guid readingId,
    BloodPressureDbContext db,
    CancellationToken cancellationToken)
{
    var entity = await db.Readings
        .Include(x => x.Symptoms)
        .ThenInclude(x => x.SymptomOption)
        .Include(x => x.TimeSlotOption)
        .Include(x => x.SportActivityOption)
        .SingleAsync(x => x.Id == readingId, cancellationToken);

    return new ReadingResponse
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
            .Where(x => x.SymptomOption is not null)
            .Select(x => new OptionItemDto { Id = x.SymptomOption!.Id, Name = x.SymptomOption!.Name })
            .ToList(),
        TimeSlot = entity.TimeSlotOption is null
            ? null
            : new OptionItemDto { Id = entity.TimeSlotOption.Id, Name = entity.TimeSlotOption.Name },
        SportActivity = entity.SportActivityOption is null
            ? null
            : new OptionItemDto { Id = entity.SportActivityOption.Id, Name = entity.SportActivityOption.Name }
    };
}
