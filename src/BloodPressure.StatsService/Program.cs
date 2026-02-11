using System.Security.Claims;
using System.Text;
using BloodPressure.Persistence;
using BloodPressure.Shared.Auth;
using BloodPressure.Shared.Contracts;
using BloodPressure.Shared.Domain;
using BloodPressure.Shared.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .Validate(options => !string.IsNullOrWhiteSpace(options.SigningKey), "Jwt:SigningKey is required.")
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

var stats = app.MapGroup("/stats")
    .RequireAuthorization();

stats.MapPost("/dashboard", async (
    StatsFilterRequest request,
    ClaimsPrincipal user,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var userId = user.GetUserId();
    var activeLicense = await db.Licenses
        .AsNoTracking()
        .Where(x => x.UserId == userId && x.IsActive)
        .OrderByDescending(x => x.EndDateUtc)
        .FirstOrDefaultAsync(cancellationToken);

    if (activeLicense is null || LicenseCalculator.IsExpired(activeLicense.Type, DateTimeOffset.UtcNow, activeLicense.EndDateUtc))
    {
        return Results.Forbid();
    }

    var to = request.ToDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
    var from = request.FromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    if (activeLicense.Type == LicenseType.Free)
    {
        var maxLookback = to.AddDays(-30);
        if (from < maxLookback)
        {
            from = maxLookback;
        }
    }

    var readings = await db.Readings
        .AsNoTracking()
        .Include(x => x.TimeSlotOption)
        .Where(x => x.UserId == userId && x.TimestampUtc >= from && x.TimestampUtc <= to)
        .ToListAsync(cancellationToken);

    var grouped = readings
        .GroupBy(x => DateOnly.FromDateTime(x.TimestampUtc.UtcDateTime))
        .OrderBy(x => x.Key)
        .ToList();

    var bpTrend = grouped
        .Select(group => new BloodPressurePointDto
        {
            Date = group.Key,
            Systolic = (decimal)group.Average(x => x.Systolic),
            Diastolic = (decimal)group.Average(x => x.Diastolic)
        })
        .ToList();

    var morningEveningTrend = readings
        .GroupBy(x => DateOnly.FromDateTime(ToCet(x.TimestampUtc).DateTime))
        .OrderBy(x => x.Key)
        .Select(group =>
        {
            var morningReadings = group.Where(IsMorningReading).ToList();
            var eveningReadings = group.Where(IsEveningReading).ToList();

            return new MorningEveningBloodPressurePointDto
            {
                Date = group.Key,
                MorningSystolic = morningReadings.Count == 0 ? null : (decimal?)morningReadings.Average(x => x.Systolic),
                MorningDiastolic = morningReadings.Count == 0 ? null : (decimal?)morningReadings.Average(x => x.Diastolic),
                EveningSystolic = eveningReadings.Count == 0 ? null : (decimal?)eveningReadings.Average(x => x.Systolic),
                EveningDiastolic = eveningReadings.Count == 0 ? null : (decimal?)eveningReadings.Average(x => x.Diastolic)
            };
        })
        .ToList();

    var hrTrend = grouped
        .Select(group => new ChartPointDto
        {
            Date = group.Key,
            Value = (decimal)group.Where(x => x.HeartRate.HasValue).DefaultIfEmpty()
                .Average(x => x?.HeartRate ?? 0)
        })
        .ToList();

    var weightTrend = readings
        .Where(x => x.WeightKg.HasValue && x.WeightKg.Value > 0m)
        .OrderBy(x => x.TimestampUtc)
        .Select(x => new ChartPointDto
        {
            Date = DateOnly.FromDateTime(x.TimestampUtc.UtcDateTime),
            Value = x.WeightKg!.Value
        })
        .ToList();

    var histogram = readings
        .GroupBy(x => x.Severity)
        .Select(group => new { Severity = group.Key, Count = group.Count() })
        .OrderBy(x => x.Severity)
        .ToList();

    var total = readings.Count == 0 ? 1 : readings.Count;
    var histogramResponse = histogram.Select(x => new HistogramBinDto
    {
        Severity = x.Severity,
        Count = x.Count,
        Percentage = Math.Round((decimal)x.Count / total * 100m, 2)
    }).ToList();

    var systolicValues = readings.Select(x => x.Systolic).ToList();
    var diastolicValues = readings.Select(x => x.Diastolic).ToList();

    var response = new DashboardStatsResponse
    {
        BloodPressureTrend = bpTrend,
        MorningEveningBloodPressureTrend = morningEveningTrend,
        HeartRateTrend = hrTrend,
        WeightTrend = weightTrend,
        SeverityHistogram = histogramResponse,
        Kpis = new KpiDto
        {
            AverageSystolic = SafeAverage(systolicValues),
            AverageDiastolic = SafeAverage(diastolicValues),
            MedianSystolic = Median(systolicValues),
            MedianDiastolic = Median(diastolicValues),
            MinSystolic = systolicValues.DefaultIfEmpty().Min(),
            MaxSystolic = systolicValues.DefaultIfEmpty().Max(),
            MinDiastolic = diastolicValues.DefaultIfEmpty().Min(),
            MaxDiastolic = diastolicValues.DefaultIfEmpty().Max(),
            StdDevSystolic = StdDev(systolicValues),
            StdDevDiastolic = StdDev(diastolicValues)
        }
    };

    return Results.Ok(response);
});

app.Run();

static bool IsMorningReading(BloodPressure.Persistence.Entities.ReadingEntity reading)
{
    var slot = NormalizeSlotName(reading.TimeSlotOption?.Name);
    if (slot.Contains("mattin", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("morning", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (slot.Contains("sera", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("evening", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var localTime = ToCet(reading.TimestampUtc).TimeOfDay;
    return localTime >= new TimeSpan(6, 0, 0) && localTime <= new TimeSpan(10, 59, 59);
}

static bool IsEveningReading(BloodPressure.Persistence.Entities.ReadingEntity reading)
{
    var slot = NormalizeSlotName(reading.TimeSlotOption?.Name);
    if (slot.Contains("sera", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("evening", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (slot.Contains("mattin", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("morning", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var localTime = ToCet(reading.TimestampUtc).TimeOfDay;
    return localTime >= new TimeSpan(20, 0, 0) && localTime <= new TimeSpan(23, 59, 59);
}

static string NormalizeSlotName(string? name) => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();

static DateTimeOffset ToCet(DateTimeOffset utc)
{
    try
    {
        var cet = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");
        return TimeZoneInfo.ConvertTime(utc, cet);
    }
    catch
    {
        try
        {
            var cetWindows = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
            return TimeZoneInfo.ConvertTime(utc, cetWindows);
        }
        catch
        {
            return utc;
        }
    }
}

static decimal SafeAverage(IReadOnlyCollection<int> values)
{
    if (values.Count == 0)
    {
        return 0m;
    }

    return (decimal)values.Average();
}

static decimal Median(IReadOnlyCollection<int> values)
{
    if (values.Count == 0)
    {
        return 0m;
    }

    var sorted = values.OrderBy(x => x).ToArray();
    var middle = sorted.Length / 2;

    if (sorted.Length % 2 == 0)
    {
        return (sorted[middle - 1] + sorted[middle]) / 2m;
    }

    return sorted[middle];
}

static decimal StdDev(IReadOnlyCollection<int> values)
{
    if (values.Count == 0)
    {
        return 0m;
    }

    var average = values.Average();
    var variance = values.Sum(value => Math.Pow(value - average, 2)) / values.Count;
    return (decimal)Math.Sqrt(variance);
}
