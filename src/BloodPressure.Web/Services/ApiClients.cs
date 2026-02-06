using System.Net.Http.Json;
using BloodPressure.Shared.Contracts;

namespace BloodPressure.Web.Services;

public sealed class AuthApiClient(HttpClient httpClient)
{
    public async Task<LoginUrlResponse> GetLoginUrlAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<LoginUrlResponse>("/auth/login-url", cancellationToken);
        return response ?? throw new InvalidOperationException("Missing login url response.");
    }
}

public sealed class ReadApiClient(HttpClient httpClient)
{
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

        var url = query.Count == 0 ? "/readings" : $"/readings?{string.Join("&", query)}";
        var response = await httpClient.GetFromJsonAsync<IReadOnlyCollection<ReadingResponse>>(url, cancellationToken);
        return response ?? Array.Empty<ReadingResponse>();
    }

    public async Task<ReadingResponse> GetReadingAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<ReadingResponse>($"/readings/{id}", cancellationToken);
        return response ?? throw new InvalidOperationException("Missing reading response.");
    }

    public async Task<OptionListResponse> GetOptionsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<OptionListResponse>("/options", cancellationToken);
        return response ?? new OptionListResponse
        {
            SymptomOptions = Array.Empty<OptionItemDto>(),
            TimeSlotOptions = Array.Empty<OptionItemDto>(),
            SportActivityOptions = Array.Empty<OptionItemDto>()
        };
    }

    public async Task<UserSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<UserSettingsResponse>("/settings/me", cancellationToken);
        return response ?? throw new InvalidOperationException("Missing settings response.");
    }

    public async Task<UserSettingsResponse> UpdateSettingsAsync(UserSettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync("/settings/me", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserSettingsResponse>(cancellationToken))!;
    }

    public async Task<ActiveLicenseResponse?> GetLicenseAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync("/licenses/me", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ActiveLicenseResponse>(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserSummaryDto>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<IReadOnlyCollection<UserSummaryDto>>("/admin/users", cancellationToken);
        return response ?? Array.Empty<UserSummaryDto>();
    }

    public async Task<UserDetailDto> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetFromJsonAsync<UserDetailDto>($"/admin/users/{id}", cancellationToken);
        return response ?? throw new InvalidOperationException("Missing user detail.");
    }

    public async Task AssignRoleAsync(Guid id, AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync($"/admin/users/{id}/role", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<LicenseDto> AssignLicenseAsync(Guid id, AssignLicenseRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync($"/admin/users/{id}/licenses", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LicenseDto>(cancellationToken))!;
    }

    public async Task<LicenseDto> UpdateLicenseAsync(Guid userId, Guid licenseId, LicenseUpdateRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync($"/admin/users/{userId}/licenses/{licenseId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LicenseDto>(cancellationToken))!;
    }
}

public sealed class WriteApiClient(HttpClient httpClient)
{
    public async Task<ReadingResponse> CreateReadingAsync(ReadingCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("/readings", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReadingResponse>(cancellationToken))!;
    }

    public async Task<ReadingResponse> UpdateReadingAsync(Guid id, ReadingUpdateRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PutAsJsonAsync($"/readings/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReadingResponse>(cancellationToken))!;
    }

    public async Task DeleteReadingAsync(Guid id, CancellationToken cancellationToken)
    {
        var response = await httpClient.DeleteAsync($"/readings/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class StatsApiClient(HttpClient httpClient)
{
    public async Task<DashboardStatsResponse> GetDashboardAsync(StatsFilterRequest request, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync("/stats/dashboard", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DashboardStatsResponse>(cancellationToken))!;
    }
}
