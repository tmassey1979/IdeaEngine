using Dragon.Backend.Contracts;

namespace Dragon.Api;

public static class DragonApiMapper
{
    public static DashboardResponse MapDashboard(BackendDashboardReadModel dashboard)
    {
        var healthyServices = dashboard.Services.Count(service => string.Equals(service.Status, "healthy", StringComparison.OrdinalIgnoreCase));
        var activeProjectCount = dashboard.Rollup.TryGetValue("inProgressIssues", out var inProgressIssues)
            ? inProgressIssues
            : 0;

        return new DashboardResponse(
            dashboard.Health,
            dashboard.AttentionSummary,
            $"{healthyServices}/{dashboard.Services.Count} healthy",
            dashboard.HostTelemetry is null
                ? null
                : new DashboardTelemetryResponse(
                    dashboard.HostTelemetry.Status,
                    dashboard.HostTelemetry.ProcessorLoadPercent,
                    dashboard.HostTelemetry.MemoryUsedPercent,
                    dashboard.HostTelemetry.Summary),
            dashboard.WaitSignal,
            dashboard.RecentLoopSummary,
            BuildQueueSummary(dashboard),
            activeProjectCount,
            dashboard.SourceStatus,
            dashboard.Services.Select(service => new ServiceResponse(service.Name, service.Status, service.Summary)).ToArray(),
            dashboard.LeadJob is null
                ? null
                : $"#{dashboard.LeadJob.IssueNumber} {dashboard.LeadJob.Action}");
    }

    public static IReadOnlyList<IdeaListItemResponse> MapIdeas(IReadOnlyList<BackendIssueReadModel> ideas)
    {
        return ideas
            .Select(idea =>
            {
                var status = MapStatus(idea.OverallStatus, idea.QueuedJobCount);
                return new IdeaListItemResponse(
                    idea.Id,
                    idea.Title,
                    status,
                    idea.OverallStatus,
                    Humanize(idea.CurrentStage),
                    QueuePositionLabel(idea.QueuedJobCount),
                    "Not exposed yet",
                    idea.LatestExecutionSummary ?? idea.WorkflowNote ?? "No project summary is available yet.",
                    status is "printing" or "review" or "blocked",
                    status == "blocked",
                    status == "blocked",
                    idea.LatestExecutionRecordedAt);
            })
            .ToArray();
    }

    public static IdeaDetailResponse MapIdeaDetail(BackendIssueDetailReadModel detail)
    {
        var status = MapStatus(detail.OverallStatus, detail.QueuedJobCount);

        return new IdeaDetailResponse(
            detail.Id,
            detail.Title,
            status,
            detail.OverallStatus,
            Humanize(detail.CurrentStage),
            QueuePositionLabel(detail.QueuedJobCount),
            "Not exposed yet",
            detail.LatestExecutionSummary ?? detail.WorkflowNote ?? "No project summary is available yet.",
            detail.Blockers,
            detail.PreferredStackLabel,
            status == "blocked",
            detail.Activity.Select(activity => new StageActivityResponse(
                activity.Stage,
                activity.Status,
                activity.ObservedAt,
                activity.Summary)).ToArray(),
            MapListPanel(detail.BacklogPanel),
            MapBoardPanel(detail.BoardPanel),
            MapActivityPanel(detail.ActivityPanel),
            detail.LatestExecutionRecordedAt);
    }

    public static IdeaFixResponse MapIssueFix(BackendIssueFixResponse response) =>
        new(
            response.Id,
            response.Title,
            response.Agent,
            response.Action,
            response.Queued,
            response.Message,
            response.OperatorInput);

    public static AgentPerformanceResponse MapAgentPerformance(BackendAgentPerformanceReadModel response) =>
        new(
            response.GeneratedAt,
            response.Summary,
            response.Agents.Select(agent => new AgentMetricResponse(
                agent.Agent,
                agent.TotalExecutions,
                agent.SuccessCount,
                agent.FailureCount,
                agent.SuccessRate,
                agent.ErrorFrequency,
                agent.AverageDurationMilliseconds,
                agent.AverageQualityScore,
                agent.AverageRetryCount,
                agent.AverageProcessorLoadPercent,
                agent.AverageMemoryUsedPercent,
                agent.AverageDiskUsedPercent,
                agent.LastRecordedAt,
                agent.Summary)).ToArray());

    public static AuditLogResponse MapAuditLog(BackendAuditLogReadModel response) =>
        new(
            response.GeneratedAt,
            response.Summary,
            response.Entries.Select(entry => new AuditLogEntryResponse(
                entry.Id,
                entry.Actor,
                entry.Action,
                entry.Project,
                entry.IssueNumber,
                entry.Details,
                entry.Source,
                entry.RecordedAt)).ToArray());

    public static ContinuousMonitoringResponse MapContinuousMonitoring(BackendContinuousMonitoringReadModel response) =>
        new(
            response.GeneratedAt,
            response.Summary,
            response.Findings.Select(finding => new ContinuousMonitoringFindingResponse(
                finding.Id,
                finding.Category,
                finding.Severity,
                finding.Status,
                finding.Project,
                finding.IssueNumber,
                finding.Summary,
                finding.Recommendation,
                finding.TriggerAutomatedUpdate,
                finding.RecordedAt,
                finding.LastObservedAt)).ToArray());

    public static MonitoringFindingUpsertResponse MapMonitoringFindingUpsert(BackendMonitoringFindingUpsertResponse response) =>
        new(
            response.Id,
            response.Category,
            response.Severity,
            response.Status,
            response.Project,
            response.IssueNumber,
            response.TriggerAutomatedUpdate,
            response.AutomatedRemediationQueued,
            response.Message);

    private static string BuildQueueSummary(BackendDashboardReadModel dashboard)
    {
        var failed = dashboard.Rollup.TryGetValue("failedIssues", out var failedIssues) ? failedIssues : 0;
        var blocked = dashboard.Rollup.TryGetValue("quarantinedIssues", out var quarantinedIssues) ? quarantinedIssues : 0;
        return $"{dashboard.QueuedJobs} queued job(s) | {failed} failed | {blocked} quarantined";
    }

    private static string MapStatus(string overallStatus, int queuedJobCount)
    {
        var normalized = overallStatus.ToLowerInvariant();
        if (normalized == "validated")
        {
            return "done";
        }

        if (normalized is "failed" or "quarantined")
        {
            return "blocked";
        }

        if (queuedJobCount > 0 && normalized != "in_progress")
        {
            return "queued";
        }

        if (normalized == "in_progress")
        {
            return "printing";
        }

        return "review";
    }

    private static string QueuePositionLabel(int queuedJobCount)
    {
        return queuedJobCount > 0
            ? $"{queuedJobCount} queued job(s)"
            : "Not exposed";
    }

    private static string Humanize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : string.Join(" ", value
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant()));
    }

    private static ListPanelResponse MapListPanel(BackendListPanelReadModel panel)
    {
        return new ListPanelResponse(
            panel.State,
            panel.Summary,
            panel.Items.Select(item => new PanelItemResponse(item.Id, item.Title, item.Status, item.Summary)).ToArray());
    }

    private static BoardPanelResponse MapBoardPanel(BackendBoardPanelReadModel panel)
    {
        return new BoardPanelResponse(
            panel.State,
            panel.Summary,
            panel.Columns.Select(column => new BoardColumnResponse(
                column.Id,
                column.Title,
                column.Cards.Select(card => new PanelItemResponse(card.Id, card.Title, card.Status, card.Summary)).ToArray())).ToArray());
    }

    private static ActivityPanelResponse MapActivityPanel(BackendActivityPanelReadModel panel)
    {
        return new ActivityPanelResponse(
            panel.State,
            panel.Summary,
            panel.Entries.Select(entry => new ActivityEntryResponse(
                entry.Id,
                entry.Title,
                entry.Status,
                entry.Summary,
                entry.RecordedAt)).ToArray());
    }
}
