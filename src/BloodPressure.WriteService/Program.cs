using System.Globalization;
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

builder.Services.AddAntiforgery(options => { options.HeaderName = "X-XSRF-TOKEN"; });

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
            .AllowAnyMethod()
            .AllowCredentials();
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
app.UseAntiforgery();

app.MapHealthChecks("/health");

var readings = app.MapGroup("/readings")
    .RequireAuthorization();

app.MapGet("/antiforgery/token", (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext httpContext) =>
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        return Results.Ok(new { token = tokens.RequestToken });
    })
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

readings.MapDelete("/", async (
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = user.GetUserId();
    var deleted = await db.Readings
        .Where(x => x.UserId == userId)
        .ExecuteDeleteAsync(cancellationToken);
    return Results.Ok(new { deletedCount = deleted });
});

readings.MapPost("/import/excel", async (
    IFormFile file,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    IOptions<ClinicalThresholdsOptions> thresholds,
    CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("File mancante o vuoto.");
    }

    var errors = new List<string>();
    var rows = ParseExcel(file, errors);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }

    var result = await ImportReadingsAsync(rows, user, db, thresholds, cancellationToken);
    return result;
});

readings.MapPost("/import/xml", async (
    IFormFile file,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    IOptions<ClinicalThresholdsOptions> thresholds,
    CancellationToken cancellationToken) =>
{
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("File mancante o vuoto.");
    }

    var errors = new List<string>();
    var rows = ParseXml(file, errors);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }

    var result = await ImportReadingsAsync(rows, user, db, thresholds, cancellationToken);
    return result;
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
            NormalLowMax = settings.Thresholds.Systolic.NormalLowMax,
            NormalOptimalMax = settings.Thresholds.Systolic.NormalOptimalMax,
            WarningHighMax = settings.Thresholds.Systolic.WarningHighMax,
            VeryHighMin = settings.Thresholds.Systolic.VeryHighMin
        },
        Diastolic = new ThresholdSet
        {
            VeryLowMax = settings.Thresholds.Diastolic.VeryLowMax,
            LowMax = settings.Thresholds.Diastolic.LowMax,
            NormalLowMax = settings.Thresholds.Diastolic.NormalLowMax,
            NormalOptimalMax = settings.Thresholds.Diastolic.NormalOptimalMax,
            WarningHighMax = settings.Thresholds.Diastolic.WarningHighMax,
            VeryHighMin = settings.Thresholds.Diastolic.VeryHighMin
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

static List<ImportReadingRow> ParseExcel(IFormFile file, List<string> errors)
{
    using var stream = file.OpenReadStream();
    using var workbook = new XLWorkbook(stream);
    var sheet = workbook.Worksheets.First();
    var headerRow = sheet.Row(1);
    var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    var lastColumn = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;
    for (var col = 1; col <= lastColumn; col++)
    {
        var header = headerRow.Cell(col).GetString().Trim();
        if (!string.IsNullOrWhiteSpace(header))
        {
            headerMap[header] = col;
        }
    }

    var hasTimestamp = headerMap.ContainsKey(ReadingImportExportColumns.TimestampUtc);
    var hasDate = headerMap.ContainsKey(ReadingImportExportColumns.DateUtc);
    var hasTime = headerMap.ContainsKey(ReadingImportExportColumns.TimeUtc);
    if (!hasTimestamp && !(hasDate && hasTime))
    {
        errors.Add("Mancano TimestampUtc oppure DateUtc e TimeUtc.");
        return [];
    }

    var rows = new List<ImportReadingRow>();
    var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

    for (var row = 2; row <= lastRow; row++)
    {
        var timestamp = ResolveTimestampFromExcel(sheet, row, headerMap, errors);
        if (!timestamp.HasValue)
        {
            continue;
        }

        if (!TryReadInt(sheet, row, headerMap, ReadingImportExportColumns.Systolic, out var systolic))
        {
            errors.Add($"Sistolica non valida alla riga {row}.");
            continue;
        }

        if (!TryReadInt(sheet, row, headerMap, ReadingImportExportColumns.Diastolic, out var diastolic))
        {
            errors.Add($"Diastolica non valida alla riga {row}.");
            continue;
        }

        var heartRate = ReadOptionalInt(sheet, row, headerMap, ReadingImportExportColumns.HeartRate);
        var weightKg = ReadOptionalDecimal(sheet, row, headerMap, ReadingImportExportColumns.WeightKg);
        var notes = ReadOptionalString(sheet, row, headerMap, ReadingImportExportColumns.Notes);
        var position = ReadEnum(sheet, row, headerMap, ReadingImportExportColumns.Position, Position.Sitting, errors);
        var medicationSkipped = ReadOptionalBool(sheet, row, headerMap, ReadingImportExportColumns.MedicationSkipped);
        var timeSlotOptionId = ReadOptionalInt(sheet, row, headerMap, ReadingImportExportColumns.TimeSlotOptionId);
        var sportActivityOptionId = ReadOptionalInt(sheet, row, headerMap, ReadingImportExportColumns.SportActivityOptionId);
        var symptomOptionIds = ReadIntList(sheet, row, headerMap, ReadingImportExportColumns.SymptomOptionIds);
        var timeSlotName = ReadOptionalString(sheet, row, headerMap, ReadingImportExportColumns.TimeSlotName);
        var sportActivityName = ReadOptionalString(sheet, row, headerMap, ReadingImportExportColumns.SportActivityName);
        var symptomOptionNames = ReadStringList(sheet, row, headerMap, ReadingImportExportColumns.SymptomOptionNames);

        rows.Add(new ImportReadingRow
        {
            Request = new ReadingCreateRequest
            {
                Systolic = systolic,
                Diastolic = diastolic,
                HeartRate = heartRate,
                WeightKg = weightKg,
                TimestampUtc = timestamp.Value,
                Notes = notes,
                Position = position,
                MedicationSkipped = medicationSkipped,
                SymptomOptionIds = symptomOptionIds,
                TimeSlotOptionId = timeSlotOptionId,
                SportActivityOptionId = sportActivityOptionId
            },
            TimeSlotName = timeSlotName,
            SportActivityName = sportActivityName,
            SymptomOptionNames = symptomOptionNames
        });
    }

    return rows;
}

static List<ImportReadingRow> ParseXml(IFormFile file, List<string> errors)
{
    var serializer = new XmlSerializer(typeof(ReadingsExportFile));
    using var stream = file.OpenReadStream();
    if (serializer.Deserialize(stream) is not ReadingsExportFile exportFile)
    {
        errors.Add("XML non valido.");
        return [];
    }

    var rows = new List<ImportReadingRow>();
    foreach (var item in exportFile.Readings)
    {
        var timestamp = item.TimestampUtc == default
            ? ResolveTimestampFromXml(item, errors)
            : item.TimestampUtc;

        rows.Add(new ImportReadingRow
        {
            Request = new ReadingCreateRequest
            {
                Systolic = item.Systolic,
                Diastolic = item.Diastolic,
                HeartRate = item.HeartRate,
                WeightKg = item.WeightKg,
                TimestampUtc = timestamp,
                Notes = item.Notes,
                Position = item.Position,
                MedicationSkipped = item.MedicationSkipped,
                SymptomOptionIds = item.SymptomOptionIds,
                TimeSlotOptionId = item.TimeSlotOptionId,
                SportActivityOptionId = item.SportActivityOptionId
            },
            TimeSlotName = item.TimeSlotName,
            SportActivityName = item.SportActivityName,
            SymptomOptionNames = item.SymptomOptionNames
        });
    }

    return rows;
}

static async Task<IResult> ImportReadingsAsync(
    List<ImportReadingRow> rows,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    IOptions<ClinicalThresholdsOptions> thresholds,
    CancellationToken cancellationToken)
{
    var errors = new List<string>();
    var userId = user.GetUserId();
    var effectiveThresholds = await ResolveThresholdsAsync(userId, db, thresholds, cancellationToken);
    var entities = new List<ReadingEntity>();
    var totalCount = rows.Count;
    var duplicateCount = 0;

    var timeSlots = await db.TimeSlotOptions.AsNoTracking().ToListAsync(cancellationToken);
    var timeSlotIds = timeSlots.Select(x => x.Id).ToHashSet();
    var timeSlotByName = timeSlots.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    var sportActivities = await db.SportActivityOptions.AsNoTracking().ToListAsync(cancellationToken);
    var sportActivityIds = sportActivities.Select(x => x.Id).ToHashSet();
    var sportActivityByName = sportActivities.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    var symptomOptions = await db.SymptomOptions.AsNoTracking().ToListAsync(cancellationToken);
    var symptomIds = symptomOptions.Select(x => x.Id).ToHashSet();
    var symptomByName = symptomOptions.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    var timestamps = rows.Select(x => x.Request.TimestampUtc).Distinct().ToList();
    var existing = await db.Readings
        .AsNoTracking()
        .Include(x => x.Symptoms)
        .Where(x => x.UserId == userId && timestamps.Contains(x.TimestampUtc))
        .ToListAsync(cancellationToken);

    var existingSignatures = new HashSet<string>(existing.Select(BuildSignatureFromEntity), StringComparer.OrdinalIgnoreCase);
    var importSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    for (var i = 0; i < rows.Count; i++)
    {
        var row = rows[i];
        var request = row.Request;

        if (request.TimeSlotOptionId.HasValue && !timeSlotIds.Contains(request.TimeSlotOptionId.Value))
        {
            errors.Add($"Riga {i + 2}: TimeSlotOptionId non valido.");
            continue;
        }

        if (!request.TimeSlotOptionId.HasValue && !string.IsNullOrWhiteSpace(row.TimeSlotName))
        {
            if (!timeSlotByName.TryGetValue(row.TimeSlotName, out var option))
            {
                errors.Add($"Riga {i + 2}: TimeSlotName non valido.");
                continue;
            }

            request = request with { TimeSlotOptionId = option.Id };
        }

        if (request.SportActivityOptionId.HasValue && !sportActivityIds.Contains(request.SportActivityOptionId.Value))
        {
            errors.Add($"Riga {i + 2}: SportActivityOptionId non valido.");
            continue;
        }

        if (!request.SportActivityOptionId.HasValue && !string.IsNullOrWhiteSpace(row.SportActivityName))
        {
            if (!sportActivityByName.TryGetValue(row.SportActivityName, out var option))
            {
                errors.Add($"Riga {i + 2}: SportActivityName non valido.");
                continue;
            }

            request = request with { SportActivityOptionId = option.Id };
        }

        if (request.SymptomOptionIds.Count > 0)
        {
            var invalidIds = request.SymptomOptionIds.Where(id => !symptomIds.Contains(id)).ToList();
            if (invalidIds.Count > 0)
            {
                errors.Add($"Riga {i + 2}: SymptomOptionIds non validi.");
                continue;
            }
        }
        else if (row.SymptomOptionNames.Count > 0)
        {
            var mappedIds = new List<int>();
            foreach (var name in row.SymptomOptionNames)
            {
                if (!symptomByName.TryGetValue(name, out var option))
                {
                    errors.Add($"Riga {i + 2}: SymptomOptionName non valido ({name}).");
                    mappedIds.Clear();
                    break;
                }

                mappedIds.Add(option.Id);
            }

            if (mappedIds.Count == 0 && errors.Count > 0)
            {
                continue;
            }

            request = request with { SymptomOptionIds = mappedIds };
        }

        if (!ReadingValidator.TryValidate(request, out var error))
        {
            errors.Add($"Riga {i + 2}: {error}");
            continue;
        }

        var signature = BuildSignatureFromRequest(request);
        if (existingSignatures.Contains(signature) || !importSignatures.Add(signature))
        {
            duplicateCount++;
            continue;
        }

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
        entities.Add(entity);
    }

    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }

    if (entities.Count > 0)
    {
        db.Readings.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(new ImportReadingsResponse
    {
        ImportedCount = entities.Count,
        SkippedDuplicates = duplicateCount,
        TotalCount = totalCount
    });
}

static DateTimeOffset? ReadDateTimeOffset(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    string columnName,
    List<string> errors)
{
    if (!headers.TryGetValue(columnName, out var column))
    {
        return null;
    }

    var cell = sheet.Cell(row, column);
    if (cell.IsEmpty())
    {
        return null;
    }

    if (cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var dt))
    {
        return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
    }

    var raw = cell.GetString();
    if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
    {
        return parsed;
    }

    errors.Add($"TimestampUtc non valido alla riga {row}.");
    return null;
}

static DateTimeOffset? ResolveTimestampFromExcel(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    List<string> errors)
{
    var timestamp = ReadDateTimeOffset(sheet, row, headers, ReadingImportExportColumns.TimestampUtc, errors);
    if (timestamp.HasValue)
    {
        return timestamp;
    }

    if (!headers.TryGetValue(ReadingImportExportColumns.DateUtc, out var dateColumn) ||
        !headers.TryGetValue(ReadingImportExportColumns.TimeUtc, out var timeColumn))
    {
        return null;
    }

    var dateRaw = sheet.Cell(row, dateColumn).GetString();
    var timeRaw = sheet.Cell(row, timeColumn).GetString();
    if (string.IsNullOrWhiteSpace(dateRaw) || string.IsNullOrWhiteSpace(timeRaw))
    {
        errors.Add($"DateUtc o TimeUtc mancanti alla riga {row}.");
        return null;
    }

    if (!DateOnly.TryParse(dateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
    {
        errors.Add($"DateUtc non valido alla riga {row}.");
        return null;
    }

    if (!TimeOnly.TryParse(timeRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
    {
        errors.Add($"TimeUtc non valido alla riga {row}.");
        return null;
    }

    var combined = date.ToDateTime(time, DateTimeKind.Utc);
    return new DateTimeOffset(combined);
}

static DateTimeOffset ResolveTimestampFromXml(ReadingExportItem item, List<string> errors)
{
    if (string.IsNullOrWhiteSpace(item.DateUtc) || string.IsNullOrWhiteSpace(item.TimeUtc))
    {
        errors.Add("DateUtc o TimeUtc mancanti nel file XML.");
        return default;
    }

    if (!DateOnly.TryParse(item.DateUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
    {
        errors.Add("DateUtc non valido nel file XML.");
        return default;
    }

    if (!TimeOnly.TryParse(item.TimeUtc, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
    {
        errors.Add("TimeUtc non valido nel file XML.");
        return default;
    }

    var combined = date.ToDateTime(time, DateTimeKind.Utc);
    return new DateTimeOffset(combined);
}

static bool TryReadInt(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    string columnName,
    out int value)
{
    value = default;
    if (!headers.TryGetValue(columnName, out var column))
    {
        return false;
    }

    var cell = sheet.Cell(row, column);
    if (cell.IsEmpty())
    {
        return false;
    }

    if (cell.TryGetValue<int>(out var intValue))
    {
        value = intValue;
        return true;
    }

    var raw = cell.GetString();
    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
}

static int? ReadOptionalInt(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    string columnName)
{
    if (!headers.TryGetValue(columnName, out var column))
    {
        return null;
    }

    var cell = sheet.Cell(row, column);
    if (cell.IsEmpty())
    {
        return null;
    }

    if (cell.TryGetValue<int>(out var intValue))
    {
        return intValue;
    }

    var raw = cell.GetString();
    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}

static decimal? ReadOptionalDecimal(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    string columnName)
{
    if (!headers.TryGetValue(columnName, out var column))
    {
        return null;
    }

    var cell = sheet.Cell(row, column);
    if (cell.IsEmpty())
    {
        return null;
    }

    if (cell.TryGetValue<decimal>(out var decimalValue))
    {
        return decimalValue;
    }

    var raw = cell.GetString();
    return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}

static string? ReadOptionalString(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    string columnName)
{
    if (!headers.TryGetValue(columnName, out var column))
    {
        return null;
    }

    var value = sheet.Cell(row, column).GetString();
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static bool ReadOptionalBool(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    string columnName)
{
    if (!headers.TryGetValue(columnName, out var column))
    {
        return false;
    }

    var cell = sheet.Cell(row, column);
    if (cell.IsEmpty())
    {
        return false;
    }

    if (cell.TryGetValue<bool>(out var boolValue))
    {
        return boolValue;
    }

    var raw = cell.GetString();
    return bool.TryParse(raw, out var parsed) && parsed;
}

static Position ReadEnum(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    string columnName,
    Position fallback,
    List<string> errors)
{
    if (!headers.TryGetValue(columnName, out var column))
    {
        return fallback;
    }

    var raw = sheet.Cell(row, column).GetString();
    if (string.IsNullOrWhiteSpace(raw))
    {
        return fallback;
    }

    if (Enum.TryParse<Position>(raw, true, out var parsed))
    {
        return parsed;
    }

    errors.Add($"Position non valido alla riga {row}.");
    return fallback;
}

static List<int> ReadIntList(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    string columnName)
{
    if (!headers.TryGetValue(columnName, out var column))
    {
        return [];
    }

    var raw = sheet.Cell(row, column).GetString();
    if (string.IsNullOrWhiteSpace(raw))
    {
        return [];
    }

    return raw
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : (int?)null)
        .Where(value => value.HasValue)
        .Select(value => value!.Value)
        .ToList();
}

static List<string> ReadStringList(
    IXLWorksheet sheet,
    int row,
    IReadOnlyDictionary<string, int> headers,
    string columnName)
{
    if (!headers.TryGetValue(columnName, out var column))
    {
        return [];
    }

    var raw = sheet.Cell(row, column).GetString();
    if (string.IsNullOrWhiteSpace(raw))
    {
        return [];
    }

    return raw
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToList();
}

static string BuildSignatureFromEntity(ReadingEntity entity)
{
    var symptomIds = entity.Symptoms.Select(x => x.SymptomOptionId).OrderBy(x => x).ToList();
    return string.Join('|',
        entity.TimestampUtc.ToString("O"),
        entity.Systolic,
        entity.Diastolic,
        entity.HeartRate,
        entity.WeightKg,
        entity.Notes ?? string.Empty,
        entity.Position,
        entity.MedicationSkipped,
        entity.TimeSlotOptionId?.ToString() ?? string.Empty,
        entity.SportActivityOptionId?.ToString() ?? string.Empty,
        string.Join(',', symptomIds));
}

static string BuildSignatureFromRequest(ReadingCreateRequest request)
{
    var symptomIds = request.SymptomOptionIds.OrderBy(x => x).ToList();
    return string.Join('|',
        request.TimestampUtc.ToString("O"),
        request.Systolic,
        request.Diastolic,
        request.HeartRate,
        request.WeightKg,
        request.Notes ?? string.Empty,
        request.Position,
        request.MedicationSkipped,
        request.TimeSlotOptionId?.ToString() ?? string.Empty,
        request.SportActivityOptionId?.ToString() ?? string.Empty,
        string.Join(',', symptomIds));
}

sealed record ImportReadingRow
{
    public required ReadingCreateRequest Request { get; init; }
    public string? TimeSlotName { get; init; }
    public string? SportActivityName { get; init; }
    public List<string> SymptomOptionNames { get; init; } = new();
}
