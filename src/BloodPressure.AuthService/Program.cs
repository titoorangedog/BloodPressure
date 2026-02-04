using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BloodPressure.AuthService.Services;
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

builder.Services.AddOptions<GoogleOAuthOptions>()
    .BindConfiguration(GoogleOAuthOptions.SectionName)
    .ValidateOnStart();

builder.Services.AddOptions<WebClientOptions>()
    .BindConfiguration(WebClientOptions.SectionName)
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
builder.Services.AddHttpClient<GoogleOAuthClient>();
builder.Services.AddScoped<JwtTokenService>();
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

app.MapGet("/auth/login-url", (GoogleOAuthClient client) =>
{
    var state = Guid.NewGuid().ToString("N");
    var url = client.BuildLoginUrl(state);
    return Results.Ok(new LoginUrlResponse { Url = url });
}).AllowAnonymous();

app.MapGet("/auth/callback", async (
    string code,
    GoogleOAuthClient oauthClient,
    JwtTokenService tokenService,
    IOptions<WebClientOptions> webClientOptions,
    IOptions<ClinicalThresholdsOptions> thresholds,
    BloodPressureDbContext db,
    CancellationToken cancellationToken) =>
{
    var payload = await oauthClient.ExchangeCodeAsync(code, cancellationToken);
    if (string.IsNullOrWhiteSpace(payload.Email))
    {
        return Results.BadRequest("Email not present in Google profile.");
    }

    var user = await db.Users
        .Include(x => x.Licenses)
        .Include(x => x.Settings)
        .SingleOrDefaultAsync(x => x.Email == payload.Email, cancellationToken);

    if (user is null)
    {
        var userId = Guid.NewGuid();
        user = new UserEntity
        {
            Id = userId,
            Email = payload.Email,
            Role = payload.Email.Equals(AuthConstants.AdminEmail, StringComparison.OrdinalIgnoreCase)
                ? UserRole.Admin
                : UserRole.User,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Settings = BuildDefaultSettings(userId, thresholds.Value)
        };

        var license = BuildNewLicense(user.Id);
        user.Licenses.Add(license);
        db.Users.Add(user);
    }
    else
    {
        if (payload.Email.Equals(AuthConstants.AdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            user.Role = UserRole.Admin;
        }

        var active = user.Licenses.SingleOrDefault(x => x.IsActive);
        if (active is null)
        {
            user.Licenses.Add(BuildNewLicense(user.Id));
        }
    }

    await db.SaveChangesAsync(cancellationToken);

    var activeLicense = user.Licenses.Single(x => x.IsActive);
    var (token, expiresAtUtc) = tokenService.CreateToken(user, activeLicense.Type);

    var redirectUrl = $"{webClientOptions.Value.BaseUrl.TrimEnd('/')}/auth-callback?token={Uri.EscapeDataString(token)}&expires={Uri.EscapeDataString(expiresAtUtc.ToString("O"))}";
    return Results.Redirect(redirectUrl);
}).AllowAnonymous();

app.MapGet("/auth/me", (ClaimsPrincipal user) =>
{
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Results.Ok(new { UserId = userId });
}).RequireAuthorization();

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

static LicenseEntity BuildNewLicense(Guid userId)
{
    var now = DateTimeOffset.UtcNow;
    return new LicenseEntity
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Type = LicenseType.Free,
        StartDateUtc = now,
        EndDateUtc = now.AddDays(90),
        IsActive = true
    };
}
