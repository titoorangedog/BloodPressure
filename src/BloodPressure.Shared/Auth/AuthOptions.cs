namespace BloodPressure.Shared.Auth;

public sealed record JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public required int AccessTokenMinutes { get; init; }
}

public sealed record GoogleOAuthOptions
{
    public const string SectionName = "GoogleOAuth";

    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string CallbackUrl { get; init; }
}
