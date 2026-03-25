using System.Net;
using System.Net.Http.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Api;

public interface IBackendReadClient
{
    Task<BackendDashboardReadModel> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<BackendIssueReadModel>> GetIdeasAsync(CancellationToken cancellationToken);
    Task<BackendIssueDetailReadModel?> GetIdeaAsync(string id, CancellationToken cancellationToken);
    Task<BackendAgentPerformanceReadModel> GetAgentPerformanceAsync(CancellationToken cancellationToken);
    Task<BackendAuditLogReadModel> GetAuditLogAsync(int limit, CancellationToken cancellationToken);
    Task<BackendContinuousMonitoringReadModel> GetContinuousMonitoringAsync(int limit, CancellationToken cancellationToken);
    Task<BackendMonitoringFindingUpsertResponse> RecordMonitoringFindingAsync(BackendMonitoringFindingUpsertRequest request, CancellationToken cancellationToken);
    Task<BackendIssueFixResponse> RequestIssueFixAsync(string id, BackendIssueFixRequest request, CancellationToken cancellationToken);
}

public sealed class BackendReadHttpClient(HttpClient httpClient) : IBackendReadClient
{
    public async Task<BackendDashboardReadModel> GetDashboardAsync(CancellationToken cancellationToken)
    {
        return await GetRequiredAsync<BackendDashboardReadModel>("/api/read/dashboard", cancellationToken);
    }

    public async Task<IReadOnlyList<BackendIssueReadModel>> GetIdeasAsync(CancellationToken cancellationToken)
    {
        return await GetRequiredAsync<BackendIssueReadModel[]>("/api/read/issues", cancellationToken) ?? [];
    }

    public async Task<BackendIssueDetailReadModel?> GetIdeaAsync(string id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => httpClient.GetAsync($"/api/read/issues/{Uri.EscapeDataString(id)}", cancellationToken),
            $"/api/read/issues/{id}",
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Backend issue detail request failed with status {(int)response.StatusCode}.", null, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<BackendIssueDetailReadModel>(cancellationToken: cancellationToken);
        return payload ?? throw new HttpRequestException("Backend issue detail response body was empty.");
    }

    public async Task<BackendAgentPerformanceReadModel> GetAgentPerformanceAsync(CancellationToken cancellationToken)
    {
        return await GetRequiredAsync<BackendAgentPerformanceReadModel>("/api/read/agent-performance", cancellationToken);
    }

    public async Task<BackendAuditLogReadModel> GetAuditLogAsync(int limit, CancellationToken cancellationToken)
    {
        return await GetRequiredAsync<BackendAuditLogReadModel>($"/api/read/audit-log?limit={Math.Clamp(limit, 1, 500)}", cancellationToken);
    }

    public async Task<BackendContinuousMonitoringReadModel> GetContinuousMonitoringAsync(int limit, CancellationToken cancellationToken)
    {
        return await GetRequiredAsync<BackendContinuousMonitoringReadModel>($"/api/read/continuous-monitoring?limit={Math.Clamp(limit, 1, 500)}", cancellationToken);
    }

    public async Task<BackendMonitoringFindingUpsertResponse> RecordMonitoringFindingAsync(BackendMonitoringFindingUpsertRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => httpClient.PostAsJsonAsync("/api/control/monitoring/findings", request, cancellationToken),
            "/api/control/monitoring/findings",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Backend monitoring control request failed with status {(int)response.StatusCode}.", null, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<BackendMonitoringFindingUpsertResponse>(cancellationToken: cancellationToken);
        return payload ?? throw new HttpRequestException("Backend monitoring control response body was empty.");
    }

    public async Task<BackendIssueFixResponse> RequestIssueFixAsync(string id, BackendIssueFixRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => httpClient.PostAsJsonAsync($"/api/control/issues/{Uri.EscapeDataString(id)}/fix", request, cancellationToken),
            $"/api/control/issues/{id}/fix",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Backend issue fix request failed with status {(int)response.StatusCode}.", null, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<BackendIssueFixResponse>(cancellationToken: cancellationToken);
        return payload ?? throw new HttpRequestException("Backend issue fix response body was empty.");
    }

    private async Task<TValue> GetRequiredAsync<TValue>(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(() => httpClient.GetAsync(path, cancellationToken), path, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Backend read request for '{path}' failed with status {(int)response.StatusCode}.", null, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<TValue>(cancellationToken: cancellationToken);
        return payload ?? throw new HttpRequestException($"Backend read request for '{path}' returned an empty response body.");
    }

    private static async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> action,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException($"Backend request for '{path}' timed out.", exception, HttpStatusCode.RequestTimeout);
        }
    }
}
