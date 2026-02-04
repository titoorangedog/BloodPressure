namespace BloodPressure.Shared.Options;

public sealed record WebClientOptions
{
    public const string SectionName = "WebClient";

    public required string BaseUrl { get; init; }
}
