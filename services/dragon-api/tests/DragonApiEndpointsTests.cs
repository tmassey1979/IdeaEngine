using System.Net;
using System.Net.Http.Json;
using Dragon.Api;
using Dragon.Backend.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dragon.Api.Tests;

public sealed class DragonApiEndpointsTests
{
    [Fact]
    public async Task DashboardEndpoint_MapsBackendDashboardPayload()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient
        {
            Dashboard = new BackendDashboardReadModel(
                "healthy",
                "0 queued job(s), 1 issue(s) in progress.",
                "github-run-watch",
                "running",
                0,
                new Dictionary<string, int>
                {
                    ["inProgressIssues"] = 1,
                    ["failedIssues"] = 0,
                    ["quarantinedIssues"] = 0
                },
                "Routine poll wait",
                "Loop is healthy.",
                new BackendLeadJobReadModel(44, "UI Dashboard", "developer", "implement_issue", "ui/react-dashboard/src/App.tsx", "ui/react-dashboard"),
                new BackendTelemetryReadModel("healthy", 4, 18, 8192, 7120, 13, "Telemetry ready"),
                [new BackendServiceReadModel("orchestrator", "healthy", "Ready")],
                "status-http")
        });

        using var client = factory.CreateClient();
        var dashboard = await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard");

        Assert.NotNull(dashboard);
        Assert.Equal("healthy", dashboard!.Health);
        Assert.Equal("1/1 healthy", dashboard.ServicesHealthyLabel);
        Assert.Equal(1, dashboard.ActiveProjectCount);
        Assert.Equal("status-http", dashboard.SourceStatus);
    }

    [Fact]
    public async Task IdeasEndpoint_MapsBackendIdeasPayload()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient
        {
            Ideas =
            [
                new BackendIssueReadModel("44", "UI Dashboard", "in_progress", "review", 0, null, "React dashboard shell was updated.", DateTimeOffset.UtcNow),
                new BackendIssueReadModel("45", "Ideas", "failed", "review", 0, "Review findings still need fixes.", "Review rejected the queue pass.", DateTimeOffset.UtcNow)
            ]
        });

        using var client = factory.CreateClient();
        var ideas = await client.GetFromJsonAsync<IdeaListItemResponse[]>("/api/ideas");

        Assert.NotNull(ideas);
        Assert.Equal(2, ideas!.Length);
        Assert.Equal("printing", ideas[0].Status);
        Assert.Equal("in_progress", ideas[0].SourceOverallStatus);
        Assert.True(ideas[0].IsActive);
        Assert.Equal("blocked", ideas[1].Status);
        Assert.True(ideas[1].IsBlocked);
        Assert.True(ideas[1].CanFix);
    }

    [Fact]
    public async Task IdeaDetailEndpoint_MapsPanelPayloads()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient
        {
            Detail = new BackendIssueDetailReadModel(
                "44",
                "UI Dashboard",
                "failed",
                "review",
                1,
                "Review findings still need fixes.",
                "React dashboard shell was updated.",
                DateTimeOffset.UtcNow,
                ["Review findings still need fixes."],
                "React + TypeScript",
                [new BackendStageActivityReadModel("Review", "failed", DateTimeOffset.UtcNow, "Review rejected the queue pass.")],
                new BackendListPanelReadModel("ready", "Backlog proxy", [new BackendPanelItemReadModel("review", "Review", "failed", "Review rejected the queue pass.")]),
                new BackendBoardPanelReadModel("ready", "Board proxy", [new BackendBoardColumnReadModel("blocked", "Blocked", [new BackendPanelItemReadModel("review", "Review", "failed", "Review rejected the queue pass.")])]),
                new BackendActivityPanelReadModel("ready", "Activity proxy", [new BackendActivityEntryReadModel("job-1", "Review", "failed", "Review rejected the queue pass.", DateTimeOffset.UtcNow)]))
        });

        using var client = factory.CreateClient();
        var detail = await client.GetFromJsonAsync<IdeaDetailResponse>("/api/ideas/44");

        Assert.NotNull(detail);
        Assert.Equal("blocked", detail!.Status);
        Assert.Equal("failed", detail.SourceOverallStatus);
        Assert.Equal("React + TypeScript", detail.PreferredStackLabel);
        Assert.True(detail.CanFix);
        Assert.Single(detail.BacklogPanel.Items);
        Assert.Single(detail.BoardPanel.Columns);
        Assert.Single(detail.ActivityPanel.Entries);
    }

    [Fact]
    public async Task IdeaFixEndpoint_ForwardsOperatorInput()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient
        {
            FixResponse = new BackendIssueFixResponse(
                "45",
                "Ideas",
                "developer",
                "implement_issue",
                true,
                "Queued developer to work issue #45 with operator guidance.",
                "Focus on review findings first.")
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/ideas/45/fix", new IdeaFixRequest("Focus on review findings first.", "terry"));
        var payload = await response.Content.ReadFromJsonAsync<IdeaFixResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.Queued);
        Assert.Equal("developer", payload.Agent);
        Assert.Equal("Focus on review findings first.", payload.OperatorInput);
    }

    [Fact]
    public async Task AgentPerformanceEndpoint_MapsBackendPayload()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient
        {
            AgentPerformance = new BackendAgentPerformanceReadModel(
                DateTimeOffset.UtcNow,
                "2 agent profile(s) aggregated from 3 execution record(s).",
                [
                    new BackendAgentMetricReadModel("developer", 2, 1, 1, 0.5, 0.5, 3000, 0.425, 0.5, 45, 50, 57, DateTimeOffset.UtcNow, "Developer summary")
                ])
        });

        using var client = factory.CreateClient();
        var payload = await client.GetFromJsonAsync<AgentPerformanceResponse>("/api/agent-performance");

        Assert.NotNull(payload);
        var agent = Assert.Single(payload!.Agents);
        Assert.Equal("developer", agent.Agent);
        Assert.Equal(2, agent.TotalExecutions);
        Assert.Equal(3000, agent.AverageDurationMilliseconds);
        Assert.Equal(45, agent.AverageProcessorLoadPercent);
    }

    [Fact]
    public async Task AuditLogEndpoint_MapsBackendPayload()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient
        {
            AuditLog = new BackendAuditLogReadModel(
                DateTimeOffset.UtcNow,
                "1 audit entry returned.",
                [
                    new BackendAuditLogEntryReadModel("entry-1", "terry", "issue_fix_requested", "DragonIdeaEngine", 35, "Queued retry.", "status-http", DateTimeOffset.UtcNow)
                ])
        });

        using var client = factory.CreateClient();
        var payload = await client.GetFromJsonAsync<AuditLogResponse>("/api/audit-log?limit=1");

        Assert.NotNull(payload);
        var entry = Assert.Single(payload!.Entries);
        Assert.Equal("terry", entry.Actor);
        Assert.Equal("issue_fix_requested", entry.Action);
        Assert.Equal(35, entry.IssueNumber);
    }

    [Fact]
    public async Task ContinuousMonitoringEndpoint_MapsBackendPayload()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient
        {
            ContinuousMonitoring = new BackendContinuousMonitoringReadModel(
                DateTimeOffset.UtcNow,
                "1 monitoring finding returned.",
                [
                    new BackendContinuousMonitoringFindingReadModel("finding-1", "new_vulnerability_discovery", "critical", "active", "DragonIdeaEngine", 52, "A dependency is vulnerable.", "Upgrade the package.", true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                ])
        });

        using var client = factory.CreateClient();
        var payload = await client.GetFromJsonAsync<ContinuousMonitoringResponse>("/api/continuous-monitoring?limit=1");

        Assert.NotNull(payload);
        var finding = Assert.Single(payload!.Findings);
        Assert.Equal("new_vulnerability_discovery", finding.Category);
        Assert.True(finding.TriggerAutomatedUpdate);
        Assert.Equal(52, finding.IssueNumber);
    }

    [Fact]
    public async Task ContinuousMonitoringControlEndpoint_ForwardsBackendRequest()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient
        {
            MonitoringFindingResponse = new BackendMonitoringFindingUpsertResponse(
                "finding-1",
                "new_vulnerability_discovery",
                "critical",
                "active",
                "DragonIdeaEngine",
                52,
                true,
                true,
                "Recorded monitoring finding 'new_vulnerability_discovery'. Automated remediation was queued.")
        });

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/continuous-monitoring/findings",
            new MonitoringFindingUpsertRequest(
                "new_vulnerability_discovery",
                "critical",
                "active",
                "DragonIdeaEngine",
                52,
                "A dependency is vulnerable.",
                "Upgrade the package.",
                true));
        var payload = await response.Content.ReadFromJsonAsync<MonitoringFindingUpsertResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.AutomatedRemediationQueued);
        Assert.True(payload.TriggerAutomatedUpdate);
        Assert.Equal("new_vulnerability_discovery", payload.Category);
    }

    [Fact]
    public async Task IdeaDetailEndpoint_ReturnsNotFoundWhenBackendReturnsNoDetail()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/ideas/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DashboardEndpoint_ReturnsServiceUnavailableWhenBackendFails()
    {
        await using var factory = new DragonApiFactory(new StubBackendReadClient
        {
            DashboardException = new HttpRequestException("backend unavailable")
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class DragonApiFactory(StubBackendReadClient backendReadClient) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackendReadClient>();
                services.AddSingleton<IBackendReadClient>(backendReadClient);
            });
        }
    }

    private sealed class StubBackendReadClient : IBackendReadClient
    {
        public BackendDashboardReadModel? Dashboard { get; init; }
        public IReadOnlyList<BackendIssueReadModel> Ideas { get; init; } = [];
        public BackendIssueDetailReadModel? Detail { get; init; }
        public BackendAgentPerformanceReadModel? AgentPerformance { get; init; }
        public BackendAuditLogReadModel? AuditLog { get; init; }
        public BackendContinuousMonitoringReadModel? ContinuousMonitoring { get; init; }
        public BackendMonitoringFindingUpsertResponse? MonitoringFindingResponse { get; init; }
        public BackendIssueFixResponse? FixResponse { get; init; }
        public Exception? DashboardException { get; init; }

        public Task<BackendDashboardReadModel> GetDashboardAsync(CancellationToken cancellationToken)
        {
            if (DashboardException is not null)
            {
                throw DashboardException;
            }

            return Task.FromResult(Dashboard ?? throw new InvalidOperationException("Dashboard payload was not configured."));
        }

        public Task<IReadOnlyList<BackendIssueReadModel>> GetIdeasAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Ideas);
        }

        public Task<BackendIssueDetailReadModel?> GetIdeaAsync(string id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Detail is not null && Detail.Id == id ? Detail : null);
        }

        public Task<BackendAgentPerformanceReadModel> GetAgentPerformanceAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(AgentPerformance ?? throw new InvalidOperationException("Agent performance payload was not configured."));
        }

        public Task<BackendAuditLogReadModel> GetAuditLogAsync(int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult(AuditLog ?? throw new InvalidOperationException("Audit log payload was not configured."));
        }

        public Task<BackendContinuousMonitoringReadModel> GetContinuousMonitoringAsync(int limit, CancellationToken cancellationToken)
        {
            return Task.FromResult(ContinuousMonitoring ?? throw new InvalidOperationException("Continuous monitoring payload was not configured."));
        }

        public Task<BackendMonitoringFindingUpsertResponse> RecordMonitoringFindingAsync(BackendMonitoringFindingUpsertRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(MonitoringFindingResponse ?? throw new InvalidOperationException("Monitoring finding response was not configured."));
        }

        public Task<BackendIssueFixResponse> RequestIssueFixAsync(string id, BackendIssueFixRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(FixResponse ?? throw new InvalidOperationException("Fix response payload was not configured."));
        }
    }
}
