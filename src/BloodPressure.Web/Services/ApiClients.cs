using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BloodPressure.Shared.Contracts;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace BloodPressure.Web.Services;

public sealed class AuthApiClient(HttpClient httpClient)
{
    public async Task<LoginUrlResponse> GetLoginUrlAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<LoginUrlResponse>("login-url", cancellationToken);
        return response ?? throw new InvalidOperationException("Missing login url response.");
    }
}

public sealed class ReadApiClient(HttpClient httpClient)
{
    public string GetExportExcelUrl() => new Uri(httpClient.BaseAddress!, "readings/export/excel").ToString();
    public string GetExportXmlUrl() => new Uri(httpClient.BaseAddress!, "readings/export/xml").ToString();
    public string GetTemplateExcelUrl() => new Uri(httpClient.BaseAddress!, "readings/template/excel").ToString();
    public string GetTemplateXmlUrl() => new Uri(httpClient.BaseAddress!, "readings/template/xml").ToString();
    public string GetExampleExcelUrl() => new Uri(httpClient.BaseAddress!, "readings/example/excel").ToString();
    public string GetExampleXmlUrl() => new Uri(httpClient.BaseAddress!, "readings/example/xml").ToString();

    public async Task<IReadOnlyCollection<ReadingResponse>> GetReadingsAsync(DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken cancellationToken)
    {
        var query = new List<string>(2);
        if (fromUtc.HasValue)
        {
            query.Add($"fromUtc={Uri.EscapeDataString(fromUtc.Value.ToString("O"))}");
        }

        if (toUtc.HasValue)
        {
            query.Add($"toUtc={Uri.EscapeDataString(toUtc.Value.ToString("O"))}");
        }

        var url = query.Count == 0 ? "readings" : $"readings?{string.Join("&", query)}";
        var response = await httpClient.GetFromJsonAsync<IReadOnlyCollection<ReadingResponse>>(url, cancellationToken);
        return response ?? Array.Empty<ReadingResponse>();
    }

    public async Task<ReadingResponse> GetReadingAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<ReadingResponse>($"readings/{id}", cancellationToken);
        return response ?? throw new InvalidOperationException("Missing reading response.");
    }

    public async Task<OptionListResponse> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<OptionListResponse>("options", cancellationToken);
        return response ?? new OptionListResponse
        {
            SymptomOptions = Array.Empty<OptionItemDto>(),
            TimeSlotOptions = Array.Empty<OptionItemDto>(),
            SportActivityOptions = Array.Empty<OptionItemDto>()
        };
    }

    public async Task<UserSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<UserSettingsResponse>("settings/me", cancellationToken);
        return response ?? throw new InvalidOperationException("Missing settings response.");
    }

    public async Task<UserSettingsResponse> UpdateSettingsAsync(UserSettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync("settings/me", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserSettingsResponse>(cancellationToken))!;
    }

    public async Task<ActiveLicenseResponse?> GetLicenseAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("licenses/me", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActiveLicenseResponse>(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<IReadOnlyCollection<UserSummaryDto>>("admin/users", cancellationToken);
        return response ?? Array.Empty<UserSummaryDto>();
    }

    public async Task<UserDetailDto> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<UserDetailDto>($"admin/users/{id}", cancellationToken);
        return response ?? throw new InvalidOperationException("Missing user detail.");
    }

    public async Task AssignRoleAsync(Guid id, AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync($"admin/users/{id}/role", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<LicenseDto> AssignLicenseAsync(Guid id, AssignLicenseRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync($"admin/users/{id}/licenses", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LicenseDto>(cancellationToken))!;
    }

    public async Task<LicenseDto> UpdateLicenseAsync(Guid userId, Guid licenseId, LicenseUpdateRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync($"admin/users/{userId}/licenses/{licenseId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LicenseDto>(cancellationToken))!;
    }
}

public sealed class WriteApiClient(HttpClient httpClient)
{
    private const long MaxUploadBytes = 50L * 1024L * 1024L;
    private string? _antiforgeryToken;

    public async Task<ReadingResponse> CreateReadingAsync(ReadingCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("readings", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReadingResponse>(cancellationToken))!;
    }

    public async Task<ReadingResponse> UpdateReadingAsync(Guid id, ReadingUpdateRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync($"readings/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReadingResponse>(cancellationToken))!;
    }

    public async Task DeleteReadingAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await httpClient.DeleteAsync($"readings/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ImportReadingsResponse> ImportExcelAsync(IBrowserFile file, CancellationToken cancellationToken)
    {
        var response = await UploadFileAsync("readings/import/excel", file, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ImportReadingsResponse>(cancellationToken))!;
    }

    public async Task<ImportReadingsResponse> ImportXmlAsync(IBrowserFile file, CancellationToken cancellationToken)
    {
        var response = await UploadFileAsync("readings/import/xml", file, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ImportReadingsResponse>(cancellationToken))!;
    }

    private async Task<HttpResponseMessage> UploadFileAsync(string url, IBrowserFile file, CancellationToken cancellationToken)
    {
        var token = await GetAntiforgeryTokenAsync(cancellationToken);
        using var content = new MultipartFormDataContent();
        var stream = file.OpenReadStream(MaxUploadBytes, cancellationToken);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        content.Add(fileContent, "file", file.Name);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        request.Headers.Add("X-XSRF-TOKEN", token);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errors = await TryReadErrorsAsync(response, cancellationToken);
            if (errors.Count > 0)
            {
                throw new ImportException(errors);
            }

            throw new ImportException(["Import fallito."]);
        }

        return response;
    }

    private async Task<string> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_antiforgeryToken))
        {
            return _antiforgeryToken!;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "antiforgery/token");
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("token", out var tokenElement) ||
            tokenElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Token antiforgery mancante.");
        }

        _antiforgeryToken = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(_antiforgeryToken))
        {
            throw new InvalidOperationException("Token antiforgery mancante.");
        }

        return _antiforgeryToken!;
    }

    private static async Task<List<string>> TryReadErrorsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("errors", out var errorsElement) &&
                errorsElement.ValueKind == JsonValueKind.Array)
            {
                var messages = errorsElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!)
                    .ToList();

                return messages;
            }
        }
        catch
        {
            return [];
        }

        return [];
    }
}

public sealed class ImportException : Exception
{
    public ImportException(IEnumerable<string> errors)
        : base("Import fallito.")
    {
        Errors = errors.ToList();
    }

    public List<string> Errors { get; }
}

public sealed class StatsApiClient(HttpClient httpClient)
{
    public async Task<DashboardStatsResponse> GetDashboardAsync(StatsFilterRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("stats/dashboard", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DashboardStatsResponse>(cancellationToken))!;
    }
}
