using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class StatusReadModelBuilder
{
    private readonly WorkflowStateStore workflowStateStore;
    private readonly ExecutionRecordStore executionRecordStore;
    private readonly AuditLogStore auditLogStore;
    private readonly MonitoringFindingStore monitoringFindingStore;

    public StatusReadModelBuilder(string rootDirectory)
    {
        workflowStateStore = new WorkflowStateStore(rootDirectory);
        executionRecordStore = new ExecutionRecordStore(rootDirectory);
        auditLogStore = new AuditLogStore(rootDirectory);
        monitoringFindingStore = new MonitoringFindingStore(rootDirectory);
    }

    public BackendDashboardReadModel BuildDashboard(StatusSnapshot snapshot)
    {
        var services = snapshot.Services?
            .Select(service => new BackendServiceReadModel(service.Name, service.Status, service.Summary))
            .ToArray() ?? [];

        return new BackendDashboardReadModel(
            snapshot.Health,
            snapshot.AttentionSummary,
            snapshot.WorkerMode,
            snapshot.WorkerState,
            snapshot.QueuedJobs,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["failedIssues"] = snapshot.Rollup.FailedIssues,
                ["quarantinedIssues"] = snapshot.Rollup.QuarantinedIssues,
                ["actionableQuarantinedIssues"] = snapshot.Rollup.ActionableQuarantinedIssues,
                ["inactiveQuarantinedIssues"] = snapshot.Rollup.InactiveQuarantinedIssues,
                ["inProgressIssues"] = snapshot.Rollup.InProgressIssues,
                ["validatedIssues"] = snapshot.Rollup.ValidatedIssues
            },
            snapshot.WaitSignal,
            snapshot.RecentLoopSignal?.Summary,
            snapshot.LeadJob is null
                ? null
                : new BackendLeadJobReadModel(
                    snapshot.LeadJob.IssueNumber,
                    snapshot.LeadJob.IssueTitle,
                    snapshot.LeadJob.Agent,
                    snapshot.LeadJob.Action,
                    snapshot.LeadJob.TargetArtifact,
                    snapshot.LeadJob.ImplementationProfile),
            snapshot.HostTelemetry is null
                ? null
                : new BackendTelemetryReadModel(
                    snapshot.HostTelemetry.Status,
                    snapshot.HostTelemetry.ProcessorCount,
                    snapshot.HostTelemetry.ProcessorLoadPercent,
                    snapshot.HostTelemetry.MemoryTotalMb,
                    snapshot.HostTelemetry.MemoryAvailableMb,
                    snapshot.HostTelemetry.MemoryUsedPercent,
                    snapshot.HostTelemetry.Summary),
            services,
            snapshot.Source);
    }

    public IReadOnlyList<BackendIssueReadModel> BuildIssues(StatusSnapshot snapshot)
    {
        return snapshot.Issues
            .Select(issue => new BackendIssueReadModel(
                issue.IssueNumber.ToString(),
                issue.IssueTitle,
                issue.OverallStatus,
                issue.CurrentStage,
                issue.QueuedJobCount,
                issue.WorkflowNote,
                issue.LatestExecutionSummary,
                issue.LatestExecutionRecordedAt))
            .ToArray();
    }

    public BackendIssueDetailReadModel? BuildIssueDetail(StatusSnapshot snapshot, int issueNumber)
    {
        var issue = snapshot.Issues.FirstOrDefault(candidate => candidate.IssueNumber == issueNumber);
        if (issue is null)
        {
            return null;
        }

        var workflows = workflowStateStore.ReadAll();
        workflows.TryGetValue(issueNumber, out var workflow);
        var executionRecords = executionRecordStore.Read(issueNumber)
            .OrderByDescending(record => record.RecordedAt)
            .ToArray();

        var activity = BuildStageActivity(workflow);
        var backlogItems = BuildBacklogItems(workflow);
        var boardColumns = BuildBoardColumns(workflow, issue.CurrentStage);
        var activityEntries = BuildActivityEntries(issue, workflow, executionRecords);

        return new BackendIssueDetailReadModel(
            issue.IssueNumber.ToString(),
            issue.IssueTitle,
            issue.OverallStatus,
            issue.CurrentStage,
            issue.QueuedJobCount,
            issue.WorkflowNote,
            issue.LatestExecutionSummary,
            issue.LatestExecutionRecordedAt,
            BuildBlockers(issue, workflow),
            DerivePreferredStackLabel(executionRecords),
            activity,
            new BackendListPanelReadModel(
                backlogItems.Count == 0 ? "empty" : "ready",
                backlogItems.Count == 0
                    ? "Backlog data is not modeled yet. Current workflow stages will populate here once more project data exists."
                    : "Workflow stages are being used as the current backlog proxy until a richer project backlog exists.",
                backlogItems),
            new BackendBoardPanelReadModel(
                activity.Count == 0 ? "empty" : "ready",
                activity.Count == 0
                    ? "Execution board data is not available yet."
                    : "Workflow stages are grouped into delivery columns for the current issue.",
                boardColumns),
            new BackendActivityPanelReadModel(
                activityEntries.Count == 0 ? "empty" : "ready",
                activityEntries.Count == 0
                    ? "No execution activity is available yet."
                    : "Workflow and execution activity for the selected issue.",
                activityEntries));
    }

    public BackendAgentPerformanceReadModel BuildAgentPerformance(StatusSnapshot snapshot)
    {
        var records = ReadAllExecutionRecords();
        var agentMetrics = records
            .GroupBy(record => record.JobAgent, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var total = group.Count();
                var successCount = group.Count(record => string.Equals(record.Status, "success", StringComparison.OrdinalIgnoreCase));
                var failureCount = total - successCount;
                var averageDuration = group
                    .Where(record => record.DurationMilliseconds is not null)
                    .Select(record => (double)record.DurationMilliseconds!.Value)
                    .DefaultIfEmpty(0d)
                    .Average();
                var averageQuality = group
                    .Where(record => record.QualityScore is not null)
                    .Select(record => record.QualityScore!.Value)
                    .DefaultIfEmpty(0d)
                    .Average();
                var averageRetryCount = group
                    .Select(record => (double)record.RetryCount)
                    .DefaultIfEmpty(0d)
                    .Average();
                var averageProcessorLoad = AverageNullable(group.Select(record => record.ProcessorLoadPercent));
                var averageMemoryUsed = AverageNullable(group.Select(record => record.MemoryUsedPercent));
                var averageDiskUsed = AverageNullable(group.Select(record => record.DiskUsedPercent));
                var lastRecordedAt = group
                    .Select(record => (DateTimeOffset?)record.RecordedAt)
                    .OrderByDescending(value => value)
                    .FirstOrDefault();
                var successRate = total == 0 ? 0d : successCount / (double)total;
                var errorFrequency = total == 0 ? 0d : failureCount / (double)total;

                return new BackendAgentMetricReadModel(
                    group.Key,
                    total,
                    successCount,
                    failureCount,
                    Math.Round(successRate, 4),
                    Math.Round(errorFrequency, 4),
                    Math.Round(averageDuration, 2),
                    Math.Round(averageQuality, 4),
                    Math.Round(averageRetryCount, 2),
                    RoundNullable(averageProcessorLoad),
                    RoundNullable(averageMemoryUsed),
                    RoundNullable(averageDiskUsed),
                    lastRecordedAt,
                    BuildAgentPerformanceSummary(group.Key, successRate, averageDuration, failureCount, averageRetryCount));
            })
            .OrderByDescending(metric => metric.ErrorFrequency)
            .ThenBy(metric => metric.SuccessRate)
            .ThenByDescending(metric => metric.TotalExecutions)
            .ThenBy(metric => metric.Agent, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var summary = agentMetrics.Length == 0
            ? "No execution records are available yet."
            : $"{agentMetrics.Length} agent profile(s) aggregated from {records.Count} execution record(s).";

        return new BackendAgentPerformanceReadModel(
            snapshot.GeneratedAt,
            summary,
            agentMetrics);
    }

    public BackendAuditLogReadModel BuildAuditLog(StatusSnapshot snapshot, int limit = 50)
    {
        var entries = auditLogStore.ReadAll()
            .OrderByDescending(entry => entry.RecordedAt)
            .Take(Math.Max(1, limit))
            .Select(entry => new BackendAuditLogEntryReadModel(
                entry.Id,
                entry.Actor,
                entry.Action,
                entry.Project,
                entry.IssueNumber,
                entry.Details,
                entry.Source,
                entry.RecordedAt))
            .ToArray();

        var summary = entries.Length == 0
            ? "No operator audit entries are available yet."
            : $"{entries.Length} audit entr{(entries.Length == 1 ? "y" : "ies")} returned.";

        return new BackendAuditLogReadModel(snapshot.GeneratedAt, summary, entries);
    }

    public BackendContinuousMonitoringReadModel BuildContinuousMonitoring(StatusSnapshot snapshot, int limit = 50)
    {
        var findings = monitoringFindingStore.ReadAll()
            .OrderByDescending(finding => finding.LastObservedAt)
            .ThenByDescending(finding => finding.RecordedAt)
            .Take(Math.Max(1, limit))
            .Select(finding => new BackendContinuousMonitoringFindingReadModel(
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
                finding.LastObservedAt))
            .ToArray();

        var summary = findings.Length == 0
            ? "No continuous monitoring findings are currently recorded."
            : $"{findings.Length} monitoring finding{(findings.Length == 1 ? string.Empty : "s")} returned.";

        return new BackendContinuousMonitoringReadModel(snapshot.GeneratedAt, summary, findings);
    }

    private static IReadOnlyList<string> BuildBlockers(IssueStatusSnapshot issue, IssueWorkflowState? workflow)
    {
        var blockers = new List<string>();

        if (!string.IsNullOrWhiteSpace(issue.WorkflowNote))
        {
            blockers.Add(issue.WorkflowNote);
        }

        if (string.Equals(issue.OverallStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("Execution failed and needs recovery or operator review.");
        }

        if (string.Equals(issue.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("Issue is quarantined and needs intervention before normal flow can resume.");
        }

        if (workflow?.ActiveRecoveryIssueNumbers?.Count > 0)
        {
            blockers.Add($"Active recovery issues: {string.Join(", ", workflow.ActiveRecoveryIssueNumbers.Select(number => $"#{number}"))}");
        }

        return blockers.Count > 0
            ? blockers
            : ["No explicit blockers are currently recorded."];
    }

    private static string DerivePreferredStackLabel(IReadOnlyList<ExecutionRecord> executionRecords)
    {
        var changedPaths = executionRecords
            .SelectMany(record => record.ChangedPaths)
            .ToArray();

        if (changedPaths.Any(path =>
                path.Contains("ui/react-dashboard", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)))
        {
            return "React + TypeScript";
        }

        if (changedPaths.Any(path =>
                path.Contains("backend/", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
        {
            return "ASP.NET Core + .NET 9";
        }

        return "Not exposed yet";
    }

    private static IReadOnlyList<BackendStageActivityReadModel> BuildStageActivity(IssueWorkflowState? workflow)
    {
        if (workflow is null)
        {
            return [];
        }

        return workflow.Stages
            .OrderByDescending(stage => stage.Value.ObservedAt)
            .ThenBy(stage => stage.Key, StringComparer.OrdinalIgnoreCase)
            .Select(stage => new BackendStageActivityReadModel(
                Humanize(stage.Key),
                stage.Value.Status,
                stage.Value.ObservedAt,
                stage.Value.Summary))
            .ToArray();
    }

    private static IReadOnlyList<BackendPanelItemReadModel> BuildBacklogItems(IssueWorkflowState? workflow)
    {
        if (workflow is null)
        {
            return [];
        }

        return workflow.Stages
            .OrderBy(stage => StageSortKey(stage.Key))
            .ThenBy(stage => stage.Key, StringComparer.OrdinalIgnoreCase)
            .Select(stage => new BackendPanelItemReadModel(
                stage.Key,
                Humanize(stage.Key),
                stage.Value.Status,
                stage.Value.Summary))
            .ToArray();
    }

    private static IReadOnlyList<BackendBoardColumnReadModel> BuildBoardColumns(IssueWorkflowState? workflow, string currentStage)
    {
        if (workflow is null)
        {
            return [
                new BackendBoardColumnReadModel("queued", "Queued", []),
                new BackendBoardColumnReadModel("in-progress", "In Progress", []),
                new BackendBoardColumnReadModel("done", "Done", []),
                new BackendBoardColumnReadModel("blocked", "Blocked", [])
            ];
        }

        var columns = new Dictionary<string, List<BackendPanelItemReadModel>>(StringComparer.OrdinalIgnoreCase)
        {
            ["queued"] = [],
            ["in-progress"] = [],
            ["done"] = [],
            ["blocked"] = []
        };

        foreach (var stage in workflow.Stages.OrderBy(entry => StageSortKey(entry.Key)).ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            var columnId = ResolveBoardColumn(stage.Key, stage.Value.Status, currentStage);
            columns[columnId].Add(new BackendPanelItemReadModel(
                stage.Key,
                Humanize(stage.Key),
                stage.Value.Status,
                stage.Value.Summary));
        }

        return [
            new BackendBoardColumnReadModel("queued", "Queued", columns["queued"]),
            new BackendBoardColumnReadModel("in-progress", "In Progress", columns["in-progress"]),
            new BackendBoardColumnReadModel("done", "Done", columns["done"]),
            new BackendBoardColumnReadModel("blocked", "Blocked", columns["blocked"])
        ];
    }

    private static IReadOnlyList<BackendActivityEntryReadModel> BuildActivityEntries(
        IssueStatusSnapshot issue,
        IssueWorkflowState? workflow,
        IReadOnlyList<ExecutionRecord> executionRecords)
    {
        var entries = new List<BackendActivityEntryReadModel>();

        entries.AddRange(executionRecords
            .Take(5)
            .Select(record => new BackendActivityEntryReadModel(
                record.JobId,
                Humanize(record.JobAgent),
                record.Status,
                record.Summary,
                record.RecordedAt)));

        if (entries.Count == 0 && workflow is not null)
        {
            entries.AddRange(workflow.Stages
                .OrderByDescending(stage => stage.Value.ObservedAt)
                .Select(stage => new BackendActivityEntryReadModel(
                    stage.Key,
                    Humanize(stage.Key),
                    stage.Value.Status,
                    stage.Value.Summary ?? $"Stage {Humanize(stage.Key)} is recorded as {stage.Value.Status}.",
                    stage.Value.ObservedAt)));
        }

        if (entries.Count == 0 && !string.IsNullOrWhiteSpace(issue.LatestExecutionSummary))
        {
            entries.Add(new BackendActivityEntryReadModel(
                $"{issue.IssueNumber}-latest",
                "Latest execution",
                issue.OverallStatus,
                issue.LatestExecutionSummary,
                issue.LatestExecutionRecordedAt));
        }

        return entries;
    }

    private IReadOnlyList<ExecutionRecord> ReadAllExecutionRecords()
    {
        if (!Directory.Exists(executionRecordStore.RunsDirectory))
        {
            return [];
        }

        return Directory
            .GetFiles(executionRecordStore.RunsDirectory, "issue-*.json", SearchOption.TopDirectoryOnly)
            .SelectMany(path =>
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (!fileName.StartsWith("issue-", StringComparison.OrdinalIgnoreCase))
                {
                    return [];
                }

                var issueText = fileName["issue-".Length..];
                return int.TryParse(issueText, out var issueNumber)
                    ? executionRecordStore.Read(issueNumber)
                    : [];
            })
            .ToArray();
    }

    private static double? AverageNullable(IEnumerable<double?> values)
    {
        var presentValues = values
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        return presentValues.Length == 0
            ? null
            : presentValues.Average();
    }

    private static double? RoundNullable(double? value) =>
        value is null ? null : Math.Round(value.Value, 2);

    private static string BuildAgentPerformanceSummary(
        string agent,
        double successRate,
        double averageDuration,
        int failureCount,
        double averageRetryCount)
    {
        return $"{Humanize(agent)} success {successRate:P0}, avg duration {averageDuration:0} ms, failures {failureCount}, avg retries {averageRetryCount:0.0}.";
    }

    private static int StageSortKey(string stage)
    {
        return stage.ToLowerInvariant() switch
        {
            "architect" => 0,
            "developer" => 1,
            "review" => 2,
            "test" => 3,
            _ => 100
        };
    }

    private static string ResolveBoardColumn(string stageName, string stageStatus, string currentStage)
    {
        if (string.Equals(stageStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return "blocked";
        }

        if (string.Equals(stageStatus, "success", StringComparison.OrdinalIgnoreCase))
        {
            return "done";
        }

        if (string.Equals(stageName, currentStage, StringComparison.OrdinalIgnoreCase))
        {
            return "in-progress";
        }

        return "queued";
    }

    private static string Humanize(string value)
    {
        return value
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant())
            .Aggregate((left, right) => $"{left} {right}");
    }
}
