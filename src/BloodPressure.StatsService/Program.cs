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

    var hrTrend = grouped
        .Select(group => new ChartPointDto
        {
            Date = group.Key,
            Value = (decimal)group.Where(x => x.HeartRate.HasValue).DefaultIfEmpty()
                .Average(x => x?.HeartRate ?? 0)
        })
        .ToList();

    var weightTrend = grouped
        .Select(group => new ChartPointDto
        {
            Date = group.Key,
            Value = (decimal)group.Where(x => x.WeightKg.HasValue).DefaultIfEmpty()
                .Average(x => x?.WeightKg ?? 0m)
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
