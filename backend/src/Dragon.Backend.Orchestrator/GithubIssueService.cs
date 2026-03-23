using System.Text.Json;
using System.Text.RegularExpressions;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class GithubIssueService
{
    private const string HeartbeatMarker = "<!-- dragon-backend-heartbeat -->";
    private const string RemediationMarker = "<!-- dragon-backend-remediation -->";
    private const string SupersededMarker = "<!-- dragon-backend-superseded -->";
    private const string RecoveryRetiredMarker = "<!-- dragon-backend-recovery-retired -->";
    private static readonly TimeSpan StalledThreshold = TimeSpan.FromMinutes(15);
    private const string RuntimeStatusRelativePath = ".dragon/status/runtime-status.json";
    private readonly GithubCommandRunner commandRunner;

    public GithubIssueService(GithubCommandRunner? commandRunner = null)
    {
        this.commandRunner = commandRunner ?? GithubCli.Run;
    }

    public IReadOnlyList<GithubIssue> ListStoryIssues(string owner, string repo, string rootDirectory)
    {
        var json = commandRunner(
            $"issue list --repo {owner}/{repo} --state open --limit 500 --json number,title,body,state,labels",
            rootDirectory
        );

        var backlogIndex = BacklogIndexLoader.Load(rootDirectory);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var issues = new List<GithubIssue>();

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (!TryMapIssue(entry, backlogIndex, out var issue))
                {
                    continue;
                }

                issues.Add(issue);
            }

            return issues
                .GroupBy(issue => issue.Number)
                .Select(group =>
                {
                    var orderedGroup = group
                        .OrderByDescending(CalculateIssueCompleteness)
                        .ThenByDescending(issue => issue.Labels.Count)
                        .ThenByDescending(issue => issue.Title.Length)
                        .ThenByDescending(issue => issue.Body.Length)
                        .ThenBy(issue => issue.Title, StringComparer.Ordinal)
                        .ThenBy(issue => issue.Body, StringComparer.Ordinal)
                        .ToArray();

                    var selected = orderedGroup[0];

                    var mergedLabels = orderedGroup
                        .SelectMany(issue => issue.Labels)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    var mergedSourceIssueNumber = orderedGroup
                        .Select(issue => issue.SourceIssueNumber)
                        .FirstOrDefault(value => value is not null);

                    var mergedHeading = orderedGroup
                        .Select(issue => issue.Heading)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

                    var mergedSourceFile = orderedGroup
                        .Select(issue => issue.SourceFile)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

                    return selected with
                    {
                        Labels = mergedLabels,
                        SourceIssueNumber = mergedSourceIssueNumber,
                        Heading = mergedHeading,
                        SourceFile = mergedSourceFile
                    };
                })
                .Where(IsSchedulableDiscoveredIssue)
                .OrderBy(issue => issue.Number)
                .ToArray();
        }
    }

    private static bool IsSchedulableDiscoveredIssue(GithubIssue issue) =>
        issue.Labels.Contains("story", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("in-progress", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("stalled", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("quarantined", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("validated", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("superseded", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("waiting-follow-up", StringComparer.OrdinalIgnoreCase);

    private static int CalculateIssueCompleteness(GithubIssue issue)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(issue.Title))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(issue.Body))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(issue.Heading))
        {
            score += 1;
        }

        if (!string.IsNullOrWhiteSpace(issue.SourceFile))
        {
            score += 1;
        }

        if (issue.SourceIssueNumber is not null)
        {
            score += 1;
        }

        return score;
    }

    private static bool TryMapIssue(
        JsonElement entry,
        IReadOnlyDictionary<string, BacklogStoryMetadata> backlogIndex,
        out GithubIssue issue)
    {
        issue = default!;

        if (!entry.TryGetProperty("state", out var stateProperty))
        {
            return false;
        }

        if (stateProperty.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var state = stateProperty.GetString() ?? "OPEN";
        if (!string.Equals(state, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!entry.TryGetProperty("labels", out var labelsProperty) || labelsProperty.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var labels = labelsProperty
            .EnumerateArray()
            .Where(label =>
                label.ValueKind == JsonValueKind.Object &&
                label.TryGetProperty("name", out var nameProperty) &&
                nameProperty.ValueKind == JsonValueKind.String)
            .Select(label => label.GetProperty("name").GetString())
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!entry.TryGetProperty("number", out var numberProperty) || numberProperty.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        if (!numberProperty.TryGetInt32(out var issueNumber))
        {
            return false;
        }

        var title = entry.TryGetProperty("title", out var titleProperty) && titleProperty.ValueKind == JsonValueKind.String
            ? titleProperty.GetString() ?? string.Empty
            : string.Empty;
        var body = entry.TryGetProperty("body", out var bodyProperty) && bodyProperty.ValueKind == JsonValueKind.String
            ? bodyProperty.GetString() ?? string.Empty
            : string.Empty;
        backlogIndex.TryGetValue(title, out var metadata);

        issue = new GithubIssue(
            issueNumber,
            title,
            state,
            labels,
            body,
            metadata?.Heading,
            metadata?.SourceFile,
            InferSourceIssueNumber(title, body)
        );

        return true;
    }

    public GithubSyncResult SyncWorkflow(
        string owner,
        string repo,
        IssueWorkflowState workflow,
        IReadOnlyList<ExecutionRecord> executionRecords,
        string rootDirectory)
    {
        if (string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase) &&
            (workflow.ActiveRecoveryIssueNumbers?.Any() ?? false))
        {
            return SyncHeartbeatWorkflow(owner, repo, workflow, executionRecords, rootDirectory);
        }

        if (string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
        {
            return SyncQuarantinedWorkflow(owner, repo, workflow, executionRecords, rootDirectory);
        }

        if (!string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase))
        {
            return SyncHeartbeatWorkflow(owner, repo, workflow, executionRecords, rootDirectory);
        }

        if (!ShouldAutoCloseValidatedWorkflow(executionRecords, rootDirectory))
        {
            return SyncHeartbeatWorkflow(owner, repo, workflow, executionRecords, rootDirectory);
        }

        var commentBody = string.Join(
            Environment.NewLine,
            [
                "Automated backend sync update:",
                $"- workflow status: {workflow.OverallStatus}",
                $"- recovery chain: {DescribeRecoveryChain(workflow)}",
                $"- stages: {string.Join(", ", workflow.Stages.Select(stage => $"{stage.Key}={stage.Value.Status}"))}",
                executionRecords.Count > 0
                    ? $"- recent executions: {string.Join("; ", executionRecords.OrderByDescending(record => record.RecordedAt).Take(3).Reverse().Select(record => $"{record.JobAgent}:{record.Status}:{record.JobId}"))}"
                    : "- recent executions: none recorded",
                GetMeaningfulChangedPaths(executionRecords, rootDirectory).Any()
                    ? $"- changed paths: {string.Join(", ", GetMeaningfulChangedPaths(executionRecords, rootDirectory))}"
                    : "- changed paths: none recorded"
            ]
        );

        EnsureLabel(owner, repo, "in-progress", "F9D0C4", "Actively being implemented.", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "quarantined", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "in-progress", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "validated", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "waiting-follow-up", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "stalled", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "superseded", rootDirectory);
        commandRunner(
            $"issue comment {workflow.IssueNumber} --repo {owner}/{repo} --body \"{EscapeForDoubleQuotes(commentBody)}\"",
            rootDirectory
        );
        commandRunner(
            $"issue close {workflow.IssueNumber} --repo {owner}/{repo} --comment \"{EscapeForDoubleQuotes("Closing issue from validated C# self-build workflow.")}\"",
            rootDirectory
        );

        return new GithubSyncResult(true, true, $"Updated GitHub issue #{workflow.IssueNumber} with execution trace.");
    }

    private static bool ShouldAutoCloseValidatedWorkflow(IReadOnlyList<ExecutionRecord> executionRecords, string rootDirectory) =>
        GetMeaningfulChangedPaths(executionRecords, rootDirectory).Any();

    private static IReadOnlyList<string> GetMeaningfulChangedPaths(
        IReadOnlyList<ExecutionRecord> executionRecords,
        string rootDirectory) =>
        executionRecords
            .SelectMany(record => record.ChangedPaths)
            .Select(path => path.Trim().Replace('\\', '/'))
            .Select(path => StripRepoRootPrefix(path, rootDirectory))
            .Select(path => path.StartsWith("./", StringComparison.Ordinal) ? path[2..] : path)
            .Select(path => Regex.Replace(path, "/{2,}", "/"))
            .Select(path => path.Replace("/./", "/", StringComparison.Ordinal))
            .Select(NormalizeRelativePathSegments)
            .Select(DecodeHtmlEntities)
            .Select(StripQueryAndFragment)
            .Select(UnwrapMarkdownPathReference)
            .Select(DecodePercentEncoding)
            .Select(StripQueryAndFragment)
            .Select(path => path.EndsWith("/.", StringComparison.Ordinal) ? path[..^2] : path)
            .Select(path => path.EndsWith("/", StringComparison.Ordinal) ? path[..^1] : path)
            .Where(IsRepoRelativeChangedPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private GithubSyncResult SyncHeartbeatWorkflow(
        string owner,
        string repo,
        IssueWorkflowState workflow,
        IReadOnlyList<ExecutionRecord> executionRecords,
        string rootDirectory)
    {
        var currentStage = InferCurrentStage(workflow);
        var currentStageState = workflow.Stages.TryGetValue(currentStage, out var stageState) ? stageState : null;
        var latestExecution = executionRecords.OrderByDescending(record => record.RecordedAt).FirstOrDefault();
        var currentStageTiming = FormatStageTiming(currentStageState?.ObservedAt, workflow.UpdatedAt);
        var stallState = DetermineStallState(currentStageState?.ObservedAt, workflow.UpdatedAt);
        var autoCloseDeferred = string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase) &&
            !ShouldAutoCloseValidatedWorkflow(executionRecords, rootDirectory);
        var releasedFromRecoveryHold = workflow.Note?.Contains("Recovery child completed", StringComparison.OrdinalIgnoreCase) == true;
        var requeuedAfterRecoveryHold = workflow.Note?.Contains("parent requeued for active flow", StringComparison.OrdinalIgnoreCase) == true;
        var retiredRecoveryIssueNumbers = releasedFromRecoveryHold && !(workflow.ActiveRecoveryIssueNumbers?.Any() ?? false)
            ? RetireReleasedRecoveryIssues(owner, repo, workflow, rootDirectory)
            : [];
        var latestExecutionTiming = latestExecution is not null
            ? $"{latestExecution.RecordedAt:O} ({FormatElapsed(workflow.UpdatedAt - latestExecution.RecordedAt)} ago)"
            : null;
        var commentBody = string.Join(
            Environment.NewLine,
            [
                HeartbeatMarker,
                "Automated backend heartbeat:",
                $"- workflow status: {workflow.OverallStatus}",
                $"- recovery chain: {DescribeRecoveryChain(workflow)}",
                $"- recovery state: {DescribeRecoveryState(workflow)}",
                $"- recovery writeback: {DescribeRecoveryWritebackState(workflow, rootDirectory)}",
                $"- worker focus: {DescribeWorkerFocus(workflow, rootDirectory)}",
                $"- worker command: {DescribeGlobalWorkerCommand(rootDirectory)}",
                $"- worker source: {DescribeGlobalWorkerSource(rootDirectory)}",
                $"- worker snapshot: {DescribeGlobalWorkerSnapshotTime(rootDirectory)}",
                $"- worker mode: {DescribeGlobalWorkerMode(rootDirectory)}",
                $"- worker state: {DescribeGlobalWorkerState(rootDirectory)}",
                $"- worker activity: {DescribeGlobalWorkerActivity(rootDirectory)}",
                $"- worker lead job: {DescribeGlobalLeadJob(rootDirectory)}",
                $"- worker lead quarantine: {DescribeGlobalLeadQuarantine(rootDirectory)}",
                $"- worker latest activity: {DescribeGlobalLatestActivity(rootDirectory)}",
                $"- worker rollup: {DescribeGlobalWorkerRollup(rootDirectory)}",
                $"- worker rollup delta: {DescribeGlobalWorkerRollupDelta(rootDirectory)}",
                $"- worker queue trend: {DescribeGlobalQueueTrend(rootDirectory)}",
                $"- worker queue compared at: {DescribeGlobalQueueComparedAt(rootDirectory)}",
                $"- worker queue compare age: {DescribeGlobalQueueCompareAge(rootDirectory)}",
                $"- worker cadence: {DescribeGlobalWorkerCadence(rootDirectory)}",
                $"- worker next poll: {DescribeGlobalWorkerNextPoll(rootDirectory)}",
                $"- worker progress: {DescribeGlobalWorkerProgress(rootDirectory)}",
                $"- worker completion: {DescribeGlobalWorkerCompletion(rootDirectory)}",
                $"- GitHub sync: {DescribeGlobalGithubSync(rootDirectory)}",
                $"- GitHub sync recorded at: {DescribeGlobalGithubSyncRecordedAt(rootDirectory)}",
                $"- GitHub sync age: {DescribeGlobalGithubSyncAge(rootDirectory)}",
                $"- GitHub replay: {DescribeGlobalGithubReplay(rootDirectory)}",
                $"- GitHub replay recorded at: {DescribeGlobalGithubReplayRecordedAt(rootDirectory)}",
                $"- GitHub replay age: {DescribeGlobalGithubReplayAge(rootDirectory)}",
                $"- pending GitHub sync count: {DescribePendingGithubSyncCount(rootDirectory)}",
                $"- pending GitHub sync issues: {DescribePendingGithubSyncIssues(rootDirectory)}",
                $"- pending GitHub sync attempts: {DescribePendingGithubSyncAttempts(rootDirectory)}",
                $"- pending GitHub sync oldest queued at: {DescribePendingGithubSyncOldestQueuedAt(rootDirectory)}",
                $"- pending GitHub sync oldest age: {DescribePendingGithubSyncOldestAge(rootDirectory)}",
                $"- pending GitHub sync last attempt: {DescribePendingGithubSyncLastAttempt(rootDirectory)}",
                $"- pending GitHub sync next retry: {DescribePendingGithubSyncNextRetry(rootDirectory)}",
                $"- pending GitHub sync: {DescribePendingGithubSyncSummary(rootDirectory)}",
                $"- latest pass: {DescribeGlobalLatestPass(rootDirectory)}",
                $"- latest pass outcome: {DescribeGlobalLatestPassOutcome(rootDirectory)}",
                $"- worker health: {DescribeGlobalWorkerHealth(rootDirectory)}",
                $"- worker attention: {DescribeGlobalWorkerAttention(rootDirectory)}",
                $"- worker loop mode: {DescribeGlobalWorkerLoopMode(rootDirectory)}",
                $"- worker loop summary: {DescribeGlobalWorkerLoopSummary(rootDirectory)}",
                $"- global intervention target: {DescribeGlobalInterventionTarget(rootDirectory)}",
                $"- intervention escalation: {DescribeGlobalInterventionEscalation(rootDirectory)}",
                $"- intervention escalation streak: {DescribeGlobalInterventionEscalationStreak(rootDirectory)}",
                $"- current stage: {currentStage}",
                $"- current stage updated: {currentStageTiming}",
                $"- stalled: {(stallState.IsStalled ? "yes" : "no")}",
                stallState.IsStalled
                    ? $"- stalled reason: current stage has been idle for {FormatElapsed(stallState.Elapsed)}"
                    : "- stalled reason: none",
                latestExecution is not null
                    ? $"- latest outcome: {latestExecution.JobAgent} {latestExecution.Status} ({latestExecution.Summary})"
                    : "- latest outcome: none recorded",
                latestExecution is not null && !string.IsNullOrWhiteSpace(latestExecution.Notes)
                    ? $"- execution notes: {latestExecution.Notes}"
                    : "- execution notes: none",
                latestExecutionTiming is not null
                    ? $"- latest execution recorded: {latestExecutionTiming}"
                    : "- latest execution recorded: none",
                $"- stages: {string.Join(", ", workflow.Stages.Select(stage => $"{stage.Key}={stage.Value.Status}"))}",
                executionRecords.Count > 0
                    ? $"- recent executions: {string.Join("; ", executionRecords.OrderByDescending(record => record.RecordedAt).Take(3).Reverse().Select(record => $"{record.JobAgent}:{record.Status}:{record.JobId}"))}"
                    : "- recent executions: none recorded",
                (workflow.ActiveRecoveryIssueNumbers?.Any() ?? false)
                    ? $"- active recovery children: {string.Join(", ", workflow.ActiveRecoveryIssueNumbers!.Select(value => $"#{value}"))}"
                    : "- active recovery children: none",
                autoCloseDeferred
                    ? "- auto-close: deferred because no execution-backed changed paths were recorded; waiting on follow-up"
                    : "- auto-close: not applicable for current workflow state",
                requeuedAfterRecoveryHold
                    ? "- recovery hold: released and parent requeued for active flow"
                    : releasedFromRecoveryHold
                    ? "- recovery hold: released; parent returned to active flow"
                    : "- recovery hold: unchanged",
                retiredRecoveryIssueNumbers.Count > 0
                    ? $"- retired recovery issues: {string.Join(", ", retiredRecoveryIssueNumbers.Select(value => $"#{value}"))}"
                    : "- retired recovery issues: none",
                workflow.Note is not null ? $"- note: {workflow.Note}" : "- note: none"
            ]
        );

        EnsureLabel(owner, repo, "in-progress", "F9D0C4", "Actively being implemented.", rootDirectory);
        EnsureLabel(owner, repo, "stalled", "C2A000", "In-progress work that appears stalled.", rootDirectory);
        EnsureLabel(owner, repo, "waiting-follow-up", "1B7F3B", "Validated by the backend loop and left open while follow-up is still needed.", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "quarantined", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "superseded", rootDirectory);
        if (string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase))
        {
            RemoveLabel(owner, repo, workflow.IssueNumber, "in-progress", rootDirectory);
            RemoveLabel(owner, repo, workflow.IssueNumber, "validated", rootDirectory);
            AddLabel(owner, repo, workflow.IssueNumber, "waiting-follow-up", rootDirectory);
        }
        else
        {
            RemoveLabel(owner, repo, workflow.IssueNumber, "validated", rootDirectory);
            RemoveLabel(owner, repo, workflow.IssueNumber, "waiting-follow-up", rootDirectory);
            AddLabel(owner, repo, workflow.IssueNumber, "in-progress", rootDirectory);
        }

        if (stallState.IsStalled)
        {
            AddLabel(owner, repo, workflow.IssueNumber, "stalled", rootDirectory);
        }
        else
        {
            RemoveLabel(owner, repo, workflow.IssueNumber, "stalled", rootDirectory);
        }

        UpsertHeartbeatComment(owner, repo, workflow.IssueNumber, commentBody, rootDirectory);
        return new GithubSyncResult(true, true, $"Updated GitHub heartbeat for issue #{workflow.IssueNumber}.");
    }

    private static StallState DetermineStallState(DateTimeOffset? observedAt, DateTimeOffset now)
    {
        if (observedAt is null)
        {
            return new StallState(false, TimeSpan.Zero);
        }

        var elapsed = now - observedAt.Value;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        return new StallState(elapsed >= StalledThreshold, elapsed);
    }

    private static string FormatStageTiming(DateTimeOffset? observedAt, DateTimeOffset now)
    {
        if (observedAt is null)
        {
            return "unknown";
        }

        return $"{observedAt.Value:O} ({FormatElapsed(now - observedAt.Value)} ago)";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalHours >= 1)
        {
            return $"{Math.Floor(elapsed.TotalHours)}h {elapsed.Minutes}m";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{Math.Floor(elapsed.TotalMinutes)}m {elapsed.Seconds}s";
        }

        return $"{Math.Max(0, elapsed.Seconds)}s";
    }

    private static string InferCurrentStage(IssueWorkflowState workflow)
    {
        if (workflow.Stages.TryGetValue("developer", out var developer) &&
            string.Equals(developer.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return "developer";
        }

        if (!workflow.Stages.ContainsKey("developer"))
        {
            return "developer";
        }

        if (workflow.Stages.TryGetValue("review", out var review) &&
            string.Equals(review.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return "review";
        }

        if (!workflow.Stages.ContainsKey("review"))
        {
            return "review";
        }

        if (workflow.Stages.TryGetValue("test", out var test) &&
            string.Equals(test.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return "test";
        }

        if (!workflow.Stages.ContainsKey("test"))
        {
            return "test";
        }

        return "complete";
    }

    private GithubSyncResult SyncQuarantinedWorkflow(
        string owner,
        string repo,
        IssueWorkflowState workflow,
        IReadOnlyList<ExecutionRecord> executionRecords,
        string rootDirectory)
    {
        MarkSupersededRecoveryIssues(owner, repo, workflow, rootDirectory);
        var currentStage = InferCurrentStage(workflow);
        var recoveryIssueNumber = EnsureRecoveryIssue(owner, repo, workflow, currentStage, executionRecords, rootDirectory);
        var commentBody = string.Join(
            Environment.NewLine,
            [
                RemediationMarker,
                "Automated backend quarantine update:",
                $"- workflow status: {workflow.OverallStatus}",
                $"- recovery chain: {DescribeRecoveryChain(workflow)}",
                $"- recovery state: {DescribeRecoveryState(workflow)}",
                $"- recovery writeback: {DescribeRecoveryWritebackState(workflow, rootDirectory)}",
                $"- worker focus: {DescribeWorkerFocus(workflow, rootDirectory)}",
                $"- worker command: {DescribeGlobalWorkerCommand(rootDirectory)}",
                $"- worker source: {DescribeGlobalWorkerSource(rootDirectory)}",
                $"- worker snapshot: {DescribeGlobalWorkerSnapshotTime(rootDirectory)}",
                $"- worker mode: {DescribeGlobalWorkerMode(rootDirectory)}",
                $"- worker state: {DescribeGlobalWorkerState(rootDirectory)}",
                $"- worker activity: {DescribeGlobalWorkerActivity(rootDirectory)}",
                $"- worker lead job: {DescribeGlobalLeadJob(rootDirectory)}",
                $"- worker lead quarantine: {DescribeGlobalLeadQuarantine(rootDirectory)}",
                $"- worker latest activity: {DescribeGlobalLatestActivity(rootDirectory)}",
                $"- worker rollup: {DescribeGlobalWorkerRollup(rootDirectory)}",
                $"- worker rollup delta: {DescribeGlobalWorkerRollupDelta(rootDirectory)}",
                $"- worker queue trend: {DescribeGlobalQueueTrend(rootDirectory)}",
                $"- worker queue compared at: {DescribeGlobalQueueComparedAt(rootDirectory)}",
                $"- worker queue compare age: {DescribeGlobalQueueCompareAge(rootDirectory)}",
                $"- worker cadence: {DescribeGlobalWorkerCadence(rootDirectory)}",
                $"- worker next poll: {DescribeGlobalWorkerNextPoll(rootDirectory)}",
                $"- worker progress: {DescribeGlobalWorkerProgress(rootDirectory)}",
                $"- worker completion: {DescribeGlobalWorkerCompletion(rootDirectory)}",
                $"- GitHub sync: {DescribeGlobalGithubSync(rootDirectory)}",
                $"- GitHub sync recorded at: {DescribeGlobalGithubSyncRecordedAt(rootDirectory)}",
                $"- GitHub sync age: {DescribeGlobalGithubSyncAge(rootDirectory)}",
                $"- GitHub replay: {DescribeGlobalGithubReplay(rootDirectory)}",
                $"- GitHub replay recorded at: {DescribeGlobalGithubReplayRecordedAt(rootDirectory)}",
                $"- GitHub replay age: {DescribeGlobalGithubReplayAge(rootDirectory)}",
                $"- pending GitHub sync count: {DescribePendingGithubSyncCount(rootDirectory)}",
                $"- pending GitHub sync issues: {DescribePendingGithubSyncIssues(rootDirectory)}",
                $"- pending GitHub sync attempts: {DescribePendingGithubSyncAttempts(rootDirectory)}",
                $"- pending GitHub sync oldest queued at: {DescribePendingGithubSyncOldestQueuedAt(rootDirectory)}",
                $"- pending GitHub sync oldest age: {DescribePendingGithubSyncOldestAge(rootDirectory)}",
                $"- pending GitHub sync last attempt: {DescribePendingGithubSyncLastAttempt(rootDirectory)}",
                $"- pending GitHub sync next retry: {DescribePendingGithubSyncNextRetry(rootDirectory)}",
                $"- pending GitHub sync: {DescribePendingGithubSyncSummary(rootDirectory)}",
                $"- latest pass: {DescribeGlobalLatestPass(rootDirectory)}",
                $"- latest pass outcome: {DescribeGlobalLatestPassOutcome(rootDirectory)}",
                $"- worker health: {DescribeGlobalWorkerHealth(rootDirectory)}",
                $"- worker attention: {DescribeGlobalWorkerAttention(rootDirectory)}",
                $"- worker loop mode: {DescribeGlobalWorkerLoopMode(rootDirectory)}",
                $"- worker loop summary: {DescribeGlobalWorkerLoopSummary(rootDirectory)}",
                $"- global intervention target: {DescribeGlobalInterventionTarget(rootDirectory)}",
                $"- intervention escalation: {DescribeGlobalInterventionEscalation(rootDirectory)}",
                $"- intervention escalation streak: {DescribeGlobalInterventionEscalationStreak(rootDirectory)}",
                $"- blocked stage: {currentStage}",
                $"- note: {workflow.Note ?? "No note recorded."}",
                recoveryIssueNumber is not null ? $"- recovery issue: #{recoveryIssueNumber}" : "- recovery issue: not created",
                $"- source issue: #{workflow.IssueNumber}",
                executionRecords.Count > 0
                    ? $"- recent failures: {string.Join("; ", executionRecords.OrderByDescending(record => record.RecordedAt).Take(3).Reverse().Select(record => $"{record.JobAgent}:{record.Status}:{record.JobId}"))}"
                    : "- recent failures: none recorded",
                GetMeaningfulChangedPaths(executionRecords, rootDirectory).Any()
                    ? $"- changed paths: {string.Join(", ", GetMeaningfulChangedPaths(executionRecords, rootDirectory))}"
                    : "- changed paths: none recorded",
                "",
                "Recovery checklist:",
                "- inspect the blocked stage and reproduce the failure or stall locally",
                "- decide whether the issue needs a narrower follow-up story or a direct fix",
                "- update this issue with the chosen recovery path before removing quarantine"
            ]
        );

        EnsureLabel(owner, repo, "quarantined", "B60205", "Repeatedly failing work that has been quarantined.", rootDirectory);
        EnsureLabel(owner, repo, "recovery", "1D76DB", "Follow-up recovery work spawned from quarantine.", rootDirectory);
        EnsureLabel(owner, repo, "superseded", "6E7781", "Older overlapping work that has been replaced by a newer path.", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "in-progress", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "stalled", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "validated", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "waiting-follow-up", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "superseded", rootDirectory);
        AddLabel(owner, repo, workflow.IssueNumber, "quarantined", rootDirectory);
        UpsertMarkedComment(owner, repo, workflow.IssueNumber, RemediationMarker, commentBody, rootDirectory);

        return new GithubSyncResult(true, true, $"Updated GitHub issue #{workflow.IssueNumber} with quarantine trace.");
    }

    private static string DescribeRecoveryChain(IssueWorkflowState workflow)
    {
        if (workflow.SourceIssueNumber is not null)
        {
            return $"parent #{workflow.SourceIssueNumber} -> current #{workflow.IssueNumber}";
        }

        if (workflow.ActiveRecoveryIssueNumbers?.Any() ?? false)
        {
            return $"current #{workflow.IssueNumber} -> children {string.Join(", ", workflow.ActiveRecoveryIssueNumbers!.Select(value => $"#{value}"))}";
        }

        return $"current #{workflow.IssueNumber}";
    }

    private static string DescribeRecoveryState(IssueWorkflowState workflow)
    {
        if (workflow.SourceIssueNumber is not null)
        {
            return $"active child issue for parent #{workflow.SourceIssueNumber}";
        }

        if (workflow.ActiveRecoveryIssueNumbers?.Any() ?? false)
        {
            return $"active recovery children {string.Join(", ", workflow.ActiveRecoveryIssueNumbers!.Select(value => $"#{value}"))}";
        }

        if (workflow.Note?.Contains("parent requeued for active flow", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "recovery hold released and parent requeued";
        }

        if (workflow.Note?.Contains("Recovery child completed", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "recovery hold released";
        }

        if (string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
        {
            return "awaiting recovery path";
        }

        return "no active recovery hold";
    }

    private static string DescribeRecoveryWritebackState(IssueWorkflowState workflow, string rootDirectory)
    {
        var pending = ReadPendingGithubSync(rootDirectory);
        if (pending.Count == 0)
        {
            return "clear";
        }

        if (workflow.SourceIssueNumber is not null)
        {
            var currentRecoveryPending = pending
                .Where(item => item.IssueNumber == workflow.IssueNumber)
                .OrderBy(item => item.RecordedAt)
                .FirstOrDefault();
            if (currentRecoveryPending is not null)
            {
                return $"retry pending for current recovery issue #{workflow.IssueNumber} ({FormatPendingGithubSyncAge(currentRecoveryPending, workflow.UpdatedAt)})";
            }

            var parentPending = pending
                .Where(item => item.IssueNumber == workflow.SourceIssueNumber.Value)
                .OrderBy(item => item.RecordedAt)
                .FirstOrDefault();
            if (parentPending is not null)
            {
                return $"retry pending for parent issue #{workflow.SourceIssueNumber.Value} ({FormatPendingGithubSyncAge(parentPending, workflow.UpdatedAt)})";
            }

            return "clear";
        }

        var activeRecoveryIssueNumbers = (workflow.ActiveRecoveryIssueNumbers ?? []).ToHashSet();
        var pendingRecoveryChildren = pending
            .Where(item => activeRecoveryIssueNumbers.Contains(item.IssueNumber))
            .OrderBy(item => item.RecordedAt)
            .ToArray();

        if (pendingRecoveryChildren.Length > 0)
        {
            if (pendingRecoveryChildren.Length == 1)
            {
                var pendingChild = pendingRecoveryChildren[0];
                return $"retry pending for recovery child #{pendingChild.IssueNumber} ({FormatPendingGithubSyncAge(pendingChild, workflow.UpdatedAt)})";
            }

            var oldestPendingChild = pendingRecoveryChildren.OrderBy(item => item.RecordedAt).First();
            return $"retry pending for recovery children {string.Join(", ", pendingRecoveryChildren.Select(value => $"#{value.IssueNumber}"))} (oldest {FormatPendingGithubSyncAge(oldestPendingChild, workflow.UpdatedAt)})";
        }

        var workflowPending = pending
            .Where(item => item.IssueNumber == workflow.IssueNumber)
            .OrderBy(item => item.RecordedAt)
            .FirstOrDefault();
        if (workflowPending is not null)
        {
            return $"retry pending for issue #{workflow.IssueNumber} ({FormatPendingGithubSyncAge(workflowPending, workflow.UpdatedAt)})";
        }

        return "clear";
    }

    private static string DescribeWorkerFocus(IssueWorkflowState workflow, string rootDirectory)
    {
        if (string.Equals(DescribeGlobalInterventionTargetKind(rootDirectory), "operator-escalation", StringComparison.OrdinalIgnoreCase))
        {
            return DescribeGlobalInterventionTargetAcknowledged(rootDirectory)
                ? "tracking acknowledged operator escalation"
                : "preparing operator escalation summary";
        }

        var writebackState = DescribeRecoveryWritebackState(workflow, rootDirectory);
        if (!string.Equals(writebackState, "clear", StringComparison.OrdinalIgnoreCase))
        {
            return "repairing GitHub writeback drift";
        }

        if ((workflow.ActiveRecoveryIssueNumbers?.Any() ?? false) ||
            workflow.SourceIssueNumber is not null ||
            string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
        {
            return "draining recovery work";
        }

        if (string.Equals(workflow.OverallStatus, "in_progress", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase))
        {
            return "shipping implementation work";
        }

        return "maintaining workflow state";
    }

    private static string? DescribeGlobalInterventionTargetKind(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("interventionTarget", out var interventionTarget) ||
                interventionTarget.ValueKind != JsonValueKind.Object ||
                !interventionTarget.TryGetProperty("kind", out var kindProperty) ||
                kindProperty.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return kindProperty.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string DescribeGlobalWorkerHealth(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("health", out var healthProperty) &&
                healthProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(healthProperty.GetString()))
            {
                return healthProperty.GetString()!;
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerMode(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("workerMode", out var modeProperty) &&
                modeProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(modeProperty.GetString()))
            {
                return modeProperty.GetString()!;
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerCommand(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("lastCommand", out var commandProperty) &&
                commandProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(commandProperty.GetString()))
            {
                return commandProperty.GetString()!;
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerSource(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("source", out var sourceProperty) &&
                sourceProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(sourceProperty.GetString()))
            {
                return sourceProperty.GetString()!;
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerSnapshotTime(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("generatedAt", out var generatedAtProperty) &&
                generatedAtProperty.ValueKind == JsonValueKind.String &&
                generatedAtProperty.TryGetDateTimeOffset(out var generatedAt))
            {
                return generatedAt.ToString("O");
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerState(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("workerState", out var stateProperty) &&
                stateProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(stateProperty.GetString()))
            {
                return stateProperty.GetString()!;
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerActivity(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("workerActivity", out var activityProperty) &&
                activityProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(activityProperty.GetString()))
            {
                return activityProperty.GetString()!;
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalLeadJob(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("leadJob", out var leadJob) ||
                leadJob.ValueKind != JsonValueKind.Object)
            {
                return "none queued";
            }

            var issueNumber = leadJob.TryGetProperty("issueNumber", out var issueNumberProperty) &&
                issueNumberProperty.ValueKind == JsonValueKind.Number &&
                issueNumberProperty.TryGetInt32(out var parsedIssueNumber)
                ? parsedIssueNumber
                : (int?)null;
            var agent = leadJob.TryGetProperty("agent", out var agentProperty) &&
                agentProperty.ValueKind == JsonValueKind.String
                ? agentProperty.GetString()
                : null;
            var action = leadJob.TryGetProperty("action", out var actionProperty) &&
                actionProperty.ValueKind == JsonValueKind.String
                ? actionProperty.GetString()
                : null;
            var targetArtifact = leadJob.TryGetProperty("targetArtifact", out var targetArtifactProperty) &&
                targetArtifactProperty.ValueKind == JsonValueKind.String
                ? targetArtifactProperty.GetString()
                : null;
            var targetOutcome = leadJob.TryGetProperty("targetOutcome", out var targetOutcomeProperty) &&
                targetOutcomeProperty.ValueKind == JsonValueKind.String
                ? targetOutcomeProperty.GetString()
                : null;
            var priority = leadJob.TryGetProperty("priority", out var priorityProperty) &&
                priorityProperty.ValueKind == JsonValueKind.String
                ? priorityProperty.GetString()
                : null;
            var workType = leadJob.TryGetProperty("workType", out var workTypeProperty) &&
                workTypeProperty.ValueKind == JsonValueKind.String
                ? workTypeProperty.GetString()
                : null;
            var blocking = leadJob.TryGetProperty("blocking", out var blockingProperty) &&
                blockingProperty.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? blockingProperty.GetBoolean()
                : (bool?)null;

            var parts = new List<string>();
            if (issueNumber is > 0)
            {
                parts.Add($"issue #{issueNumber.Value}");
            }

            if (!string.IsNullOrWhiteSpace(agent) || !string.IsNullOrWhiteSpace(action))
            {
                parts.Add(string.Join(":", new[] { agent, action }.Where(value => !string.IsNullOrWhiteSpace(value))));
            }

            if (!string.IsNullOrWhiteSpace(targetArtifact))
            {
                parts.Add(targetArtifact!);
            }

            if (!string.IsNullOrWhiteSpace(targetOutcome))
            {
                parts.Add(targetOutcome!);
            }

            if (!string.IsNullOrWhiteSpace(priority))
            {
                parts.Add($"priority {priority}");
            }

            if (blocking is true)
            {
                parts.Add("blocking");
            }

            if (!string.IsNullOrWhiteSpace(workType))
            {
                parts.Add(workType!);
            }

            return parts.Count == 0
                ? "none queued"
                : string.Join(" · ", parts);
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalLeadQuarantine(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("leadQuarantine", out var leadQuarantine) ||
                leadQuarantine.ValueKind != JsonValueKind.Object)
            {
                return "none active";
            }

            var issueNumber = leadQuarantine.TryGetProperty("issueNumber", out var issueNumberProperty) &&
                issueNumberProperty.ValueKind == JsonValueKind.Number &&
                issueNumberProperty.TryGetInt32(out var parsedIssueNumber)
                ? parsedIssueNumber
                : (int?)null;
            var recoveryIssueNumber = leadQuarantine.TryGetProperty("recoveryIssueNumber", out var recoveryIssueNumberProperty) &&
                recoveryIssueNumberProperty.ValueKind == JsonValueKind.Number &&
                recoveryIssueNumberProperty.TryGetInt32(out var parsedRecoveryIssueNumber)
                ? parsedRecoveryIssueNumber
                : (int?)null;
            var queuedRecoveryJobs = leadQuarantine.TryGetProperty("queuedRecoveryJobs", out var queuedRecoveryJobsProperty) &&
                queuedRecoveryJobsProperty.ValueKind == JsonValueKind.Number &&
                queuedRecoveryJobsProperty.TryGetInt32(out var parsedQueuedRecoveryJobs)
                ? parsedQueuedRecoveryJobs
                : 0;
            var state = leadQuarantine.TryGetProperty("state", out var stateProperty) &&
                stateProperty.ValueKind == JsonValueKind.String
                ? stateProperty.GetString()
                : null;
            var summary = leadQuarantine.TryGetProperty("summary", out var summaryProperty) &&
                summaryProperty.ValueKind == JsonValueKind.String
                ? summaryProperty.GetString()
                : null;

            var parts = new List<string>();
            if (issueNumber is > 0)
            {
                parts.Add($"issue #{issueNumber.Value}");
            }

            if (recoveryIssueNumber is > 0)
            {
                parts.Add($"recovery #{recoveryIssueNumber.Value}");
            }

            if (queuedRecoveryJobs > 0)
            {
                parts.Add($"{queuedRecoveryJobs} queued recovery job{(queuedRecoveryJobs == 1 ? string.Empty : "s")}");
            }

            if (!string.IsNullOrWhiteSpace(state))
            {
                parts.Add(state!);
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                parts.Add(summary!);
            }

            return parts.Count == 0
                ? "none active"
                : string.Join(" · ", parts);
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalLatestActivity(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("latestActivity", out var latestActivity) ||
                latestActivity.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            var issueNumber = latestActivity.TryGetProperty("issueNumber", out var issueNumberProperty) &&
                issueNumberProperty.ValueKind == JsonValueKind.Number &&
                issueNumberProperty.TryGetInt32(out var parsedIssueNumber)
                ? parsedIssueNumber
                : (int?)null;
            var currentStage = latestActivity.TryGetProperty("currentStage", out var currentStageProperty) &&
                currentStageProperty.ValueKind == JsonValueKind.String
                ? currentStageProperty.GetString()
                : null;
            var summary = latestActivity.TryGetProperty("summary", out var summaryProperty) &&
                summaryProperty.ValueKind == JsonValueKind.String
                ? summaryProperty.GetString()
                : null;
            var recordedAt = latestActivity.TryGetProperty("recordedAt", out var recordedAtProperty) &&
                recordedAtProperty.ValueKind == JsonValueKind.String &&
                recordedAtProperty.TryGetDateTimeOffset(out var parsedRecordedAt)
                ? parsedRecordedAt
                : (DateTimeOffset?)null;

            var parts = new List<string>();
            if (issueNumber is > 0)
            {
                parts.Add($"issue #{issueNumber.Value}");
            }

            if (!string.IsNullOrWhiteSpace(currentStage))
            {
                parts.Add($"stage {currentStage}");
            }

            if (!string.IsNullOrWhiteSpace(summary))
            {
                parts.Add(summary!);
            }

            if (recordedAt is not null)
            {
                parts.Add(recordedAt.Value.ToString("O"));
            }

            return parts.Count == 0
                ? "not recorded"
                : string.Join(" · ", parts);
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerRollup(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            var queuedJobs = root.TryGetProperty("queuedJobs", out var queuedJobsProperty) &&
                queuedJobsProperty.ValueKind == JsonValueKind.Number &&
                queuedJobsProperty.TryGetInt32(out var parsedQueuedJobs)
                ? parsedQueuedJobs
                : 0;

            if (!root.TryGetProperty("rollup", out var rollup) || rollup.ValueKind != JsonValueKind.Object)
            {
                return $"queued {queuedJobs}";
            }

            var failedIssues = ReadInt32Property(rollup, "failedIssues");
            var quarantinedIssues = ReadInt32Property(rollup, "quarantinedIssues");
            var actionableQuarantinedIssues = ReadInt32Property(rollup, "actionableQuarantinedIssues");
            var inactiveQuarantinedIssues = ReadInt32Property(rollup, "inactiveQuarantinedIssues");
            var inProgressIssues = ReadInt32Property(rollup, "inProgressIssues");
            var validatedIssues = ReadInt32Property(rollup, "validatedIssues");

            return $"queued {queuedJobs} · in-progress {inProgressIssues} · failed {failedIssues} · quarantined {quarantinedIssues} ({actionableQuarantinedIssues} actionable, {inactiveQuarantinedIssues} inactive) · validated {validatedIssues}";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerRollupDelta(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("rollupDelta", out var rollupDelta) || rollupDelta.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            var failedIssues = ReadInt32Property(rollupDelta, "failedIssues");
            var quarantinedIssues = ReadInt32Property(rollupDelta, "quarantinedIssues");
            var inProgressIssues = ReadInt32Property(rollupDelta, "inProgressIssues");
            var validatedIssues = ReadInt32Property(rollupDelta, "validatedIssues");

            return $"failed {FormatSignedDelta(failedIssues)} · quarantined {FormatSignedDelta(quarantinedIssues)} · in-progress {FormatSignedDelta(inProgressIssues)} · validated {FormatSignedDelta(validatedIssues)}";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string FormatSignedDelta(int value) =>
        value >= 0
            ? $"+{value}"
            : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string DescribeGlobalQueueTrend(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            var direction = root.TryGetProperty("queueDirection", out var directionProperty) &&
                directionProperty.ValueKind == JsonValueKind.String
                ? directionProperty.GetString()
                : null;
            var delta = root.TryGetProperty("queueDelta", out var deltaProperty) &&
                deltaProperty.ValueKind == JsonValueKind.Number &&
                deltaProperty.TryGetInt32(out var parsedDelta)
                ? parsedDelta
                : (int?)null;
            var comparedAt = root.TryGetProperty("queueComparedAt", out var comparedAtProperty) &&
                comparedAtProperty.ValueKind == JsonValueKind.String &&
                comparedAtProperty.TryGetDateTimeOffset(out var parsedComparedAt)
                ? parsedComparedAt
                : (DateTimeOffset?)null;

            if (string.IsNullOrWhiteSpace(direction) && delta is null && comparedAt is null)
            {
                return "not recorded";
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(direction))
            {
                parts.Add(direction!);
            }

            if (delta is not null)
            {
                parts.Add(delta.Value >= 0
                    ? $"+{delta.Value}"
                    : delta.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (comparedAt is not null)
            {
                parts.Add($"vs {comparedAt.Value:O}");
            }

            return parts.Count == 0
                ? "not recorded"
                : string.Join(" · ", parts);
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalQueueComparedAt(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            var comparedAt = root.TryGetProperty("queueComparedAt", out var comparedAtProperty) &&
                comparedAtProperty.ValueKind == JsonValueKind.String &&
                comparedAtProperty.TryGetDateTimeOffset(out var parsedComparedAt)
                ? parsedComparedAt
                : (DateTimeOffset?)null;

            return comparedAt is null
                ? "not recorded"
                : comparedAt.Value.ToString("O");
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalQueueCompareAge(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            var generatedAt = root.TryGetProperty("generatedAt", out var generatedAtProperty) &&
                generatedAtProperty.ValueKind == JsonValueKind.String &&
                generatedAtProperty.TryGetDateTimeOffset(out var parsedGeneratedAt)
                ? parsedGeneratedAt
                : (DateTimeOffset?)null;
            var comparedAt = root.TryGetProperty("queueComparedAt", out var comparedAtProperty) &&
                comparedAtProperty.ValueKind == JsonValueKind.String &&
                comparedAtProperty.TryGetDateTimeOffset(out var parsedComparedAt)
                ? parsedComparedAt
                : (DateTimeOffset?)null;

            if (generatedAt is null || comparedAt is null)
            {
                return "not recorded";
            }

            return FormatElapsed(generatedAt.Value - comparedAt.Value);
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static int ReadInt32Property(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetInt32(out var parsedValue)
            ? parsedValue
            : 0;

    private static string DescribeGlobalWorkerCadence(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            var pollIntervalSeconds = root.TryGetProperty("pollIntervalSeconds", out var cadenceProperty) &&
                cadenceProperty.ValueKind == JsonValueKind.Number &&
                cadenceProperty.TryGetInt32(out var seconds)
                ? seconds
                : (int?)null;
            var nextPollAt = root.TryGetProperty("nextPollAt", out var nextPollProperty) &&
                nextPollProperty.ValueKind == JsonValueKind.String &&
                nextPollProperty.TryGetDateTimeOffset(out var nextPoll)
                ? nextPoll
                : (DateTimeOffset?)null;

            if (pollIntervalSeconds is null && nextPollAt is null)
            {
                return "not scheduled";
            }

            if (pollIntervalSeconds is not null && nextPollAt is not null)
            {
                return $"every {pollIntervalSeconds.Value} second{(pollIntervalSeconds.Value == 1 ? string.Empty : "s")}, next poll {nextPollAt.Value:O}";
            }

            if (pollIntervalSeconds is not null)
            {
                return $"every {pollIntervalSeconds.Value} second{(pollIntervalSeconds.Value == 1 ? string.Empty : "s")}";
            }

            return $"next poll {nextPollAt!.Value:O}";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerNextPoll(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not scheduled";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("nextPollAt", out var nextPollProperty) &&
                nextPollProperty.ValueKind == JsonValueKind.String &&
                nextPollProperty.TryGetDateTimeOffset(out var nextPollAt))
            {
                return nextPollAt.ToString("O");
            }

            return "not scheduled";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerProgress(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;

            var currentPassNumber = root.TryGetProperty("currentPassNumber", out var currentPassProperty) &&
                currentPassProperty.ValueKind == JsonValueKind.Number &&
                currentPassProperty.TryGetInt32(out var currentPass)
                ? currentPass
                : 0;
            var maxPasses = root.TryGetProperty("maxPasses", out var maxPassesProperty) &&
                maxPassesProperty.ValueKind == JsonValueKind.Number &&
                maxPassesProperty.TryGetInt32(out var parsedMaxPasses)
                ? parsedMaxPasses
                : (int?)null;
            var idleStreak = root.TryGetProperty("idleStreak", out var idleStreakProperty) &&
                idleStreakProperty.ValueKind == JsonValueKind.Number &&
                idleStreakProperty.TryGetInt32(out var parsedIdleStreak)
                ? parsedIdleStreak
                : 0;
            var idleTarget = root.TryGetProperty("idleTarget", out var idleTargetProperty) &&
                idleTargetProperty.ValueKind == JsonValueKind.Number &&
                idleTargetProperty.TryGetInt32(out var parsedIdleTarget)
                ? parsedIdleTarget
                : 0;
            var idlePassesRemaining = root.TryGetProperty("idlePassesRemaining", out var idleRemainingProperty) &&
                idleRemainingProperty.ValueKind == JsonValueKind.Number &&
                idleRemainingProperty.TryGetInt32(out var parsedIdleRemaining)
                ? parsedIdleRemaining
                : (int?)null;
            var passBudgetRemaining = root.TryGetProperty("passBudgetRemaining", out var budgetProperty) &&
                budgetProperty.ValueKind == JsonValueKind.Number &&
                budgetProperty.TryGetInt32(out var parsedBudget)
                ? parsedBudget
                : (int?)null;

            var passLabel = maxPasses is > 0
                ? $"{currentPassNumber} / {maxPasses.Value}"
                : currentPassNumber > 0
                    ? currentPassNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : "n/a";
            var idleLabel = idleTarget > 0
                ? $"{idleStreak} / {idleTarget}"
                : idleStreak.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var remainingLabel = idlePassesRemaining?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a";
            var budgetLabel = passBudgetRemaining?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a";

            return $"pass {passLabel} · idle {idleLabel} · remaining {remainingLabel} · budget {budgetLabel}";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerCompletion(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            var workerState = root.TryGetProperty("workerState", out var workerStateProperty) &&
                workerStateProperty.ValueKind == JsonValueKind.String
                ? workerStateProperty.GetString()
                : null;
            var reason = root.TryGetProperty("workerCompletionReason", out var reasonProperty) &&
                reasonProperty.ValueKind == JsonValueKind.String
                ? reasonProperty.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(reason))
            {
                return workerState switch
                {
                    "waiting" or "running" => "active",
                    "complete" => "complete",
                    "snapshot" => "not recorded",
                    _ => "not recorded"
                };
            }

            return reason switch
            {
                "idle_target_reached" => "idle target reached",
                "idle_run_completed" => "idle run completed",
                "max_passes_reached" => "pass cap reached",
                "max_cycles_reached" => "cycle cap reached",
                _ => reason.Replace("_", " ", StringComparison.Ordinal)
            };
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalGithubSync(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("latestGithubSync", out var latestGithubSync) ||
                latestGithubSync.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            var issueNumber = latestGithubSync.TryGetProperty("issueNumber", out var issueNumberProperty) &&
                issueNumberProperty.ValueKind == JsonValueKind.Number &&
                issueNumberProperty.TryGetInt32(out var parsedIssueNumber)
                ? parsedIssueNumber
                : (int?)null;
            var attempted = latestGithubSync.TryGetProperty("attempted", out var attemptedProperty) &&
                attemptedProperty.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? attemptedProperty.GetBoolean()
                : (bool?)null;
            var updated = latestGithubSync.TryGetProperty("updated", out var updatedProperty) &&
                updatedProperty.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? updatedProperty.GetBoolean()
                : (bool?)null;
            var summary = latestGithubSync.TryGetProperty("summary", out var summaryProperty) &&
                summaryProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(summaryProperty.GetString())
                ? summaryProperty.GetString()!
                : "summary unavailable";

            var prefix = issueNumber is > 0
                ? $"issue #{issueNumber.Value}"
                : "latest result";
            var state = attempted switch
            {
                false => "not attempted",
                true when updated is true => "updated",
                true when updated is false => "pending",
                _ => "recorded"
            };

            return $"{prefix} {state}: {summary}";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalGithubSyncRecordedAt(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("latestGithubSync", out var latestGithubSync) ||
                latestGithubSync.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            return latestGithubSync.TryGetProperty("recordedAt", out var recordedAtProperty) &&
                recordedAtProperty.ValueKind == JsonValueKind.String &&
                recordedAtProperty.TryGetDateTimeOffset(out var recordedAt)
                ? recordedAt.ToString("O")
                : "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalGithubSyncAge(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("latestGithubSync", out var latestGithubSync) ||
                latestGithubSync.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            var recordedAt = latestGithubSync.TryGetProperty("recordedAt", out var recordedAtProperty) &&
                recordedAtProperty.ValueKind == JsonValueKind.String &&
                recordedAtProperty.TryGetDateTimeOffset(out var parsedRecordedAt)
                ? parsedRecordedAt
                : (DateTimeOffset?)null;
            var generatedAt = root.TryGetProperty("generatedAt", out var generatedAtProperty) &&
                generatedAtProperty.ValueKind == JsonValueKind.String &&
                generatedAtProperty.TryGetDateTimeOffset(out var parsedGeneratedAt)
                ? parsedGeneratedAt
                : (DateTimeOffset?)null;

            return recordedAt is null || generatedAt is null
                ? "not recorded"
                : FormatElapsed(generatedAt.Value - recordedAt.Value);
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalGithubReplay(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("latestGithubReplay", out var latestGithubReplay) ||
                latestGithubReplay.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            var attemptedCount = latestGithubReplay.TryGetProperty("attemptedCount", out var attemptedCountProperty) &&
                attemptedCountProperty.ValueKind == JsonValueKind.Number &&
                attemptedCountProperty.TryGetInt32(out var parsedAttemptedCount)
                ? parsedAttemptedCount
                : 0;
            var updatedCount = latestGithubReplay.TryGetProperty("updatedCount", out var updatedCountProperty) &&
                updatedCountProperty.ValueKind == JsonValueKind.Number &&
                updatedCountProperty.TryGetInt32(out var parsedUpdatedCount)
                ? parsedUpdatedCount
                : 0;
            var failedCount = latestGithubReplay.TryGetProperty("failedCount", out var failedCountProperty) &&
                failedCountProperty.ValueKind == JsonValueKind.Number &&
                failedCountProperty.TryGetInt32(out var parsedFailedCount)
                ? parsedFailedCount
                : 0;
            var summary = latestGithubReplay.TryGetProperty("summary", out var summaryProperty) &&
                summaryProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(summaryProperty.GetString())
                ? summaryProperty.GetString()!
                : null;

            var counts = $"{updatedCount}/{attemptedCount} updated";
            if (failedCount > 0)
            {
                counts += $", {failedCount} failed";
            }

            return string.IsNullOrWhiteSpace(summary)
                ? counts
                : $"{counts}: {summary}";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalGithubReplayRecordedAt(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("latestGithubReplay", out var latestGithubReplay) ||
                latestGithubReplay.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            return latestGithubReplay.TryGetProperty("recordedAt", out var recordedAtProperty) &&
                recordedAtProperty.ValueKind == JsonValueKind.String &&
                recordedAtProperty.TryGetDateTimeOffset(out var recordedAt)
                ? recordedAt.ToString("O")
                : "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalGithubReplayAge(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("latestGithubReplay", out var latestGithubReplay) ||
                latestGithubReplay.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            var recordedAt = latestGithubReplay.TryGetProperty("recordedAt", out var recordedAtProperty) &&
                recordedAtProperty.ValueKind == JsonValueKind.String &&
                recordedAtProperty.TryGetDateTimeOffset(out var parsedRecordedAt)
                ? parsedRecordedAt
                : (DateTimeOffset?)null;
            var generatedAt = root.TryGetProperty("generatedAt", out var generatedAtProperty) &&
                generatedAtProperty.ValueKind == JsonValueKind.String &&
                generatedAtProperty.TryGetDateTimeOffset(out var parsedGeneratedAt)
                ? parsedGeneratedAt
                : (DateTimeOffset?)null;

            return recordedAt is null || generatedAt is null
                ? "not recorded"
                : FormatElapsed(generatedAt.Value - recordedAt.Value);
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribePendingGithubSyncSummary(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("pendingGithubSyncSummary", out var summaryProperty) &&
                summaryProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(summaryProperty.GetString()))
            {
                return summaryProperty.GetString()!;
            }

            if (document.RootElement.TryGetProperty("pendingGithubSyncCount", out var countProperty) &&
                countProperty.ValueKind == JsonValueKind.Number &&
                countProperty.TryGetInt32(out var count))
            {
                return count == 0
                    ? "clear"
                    : $"{count} pending update(s)";
            }

            return "clear";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribePendingGithubSyncCount(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            return document.RootElement.TryGetProperty("pendingGithubSyncCount", out var countProperty) &&
                countProperty.ValueKind == JsonValueKind.Number &&
                countProperty.TryGetInt32(out var count)
                ? count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribePendingGithubSyncNextRetry(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not scheduled";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("pendingGithubSync", out var pendingGithubSync) ||
                pendingGithubSync.ValueKind != JsonValueKind.Array)
            {
                return "not scheduled";
            }

            var nextRetry = pendingGithubSync
                .EnumerateArray()
                .Select(item =>
                    item.TryGetProperty("nextRetryAt", out var nextRetryAtProperty) &&
                    nextRetryAtProperty.ValueKind == JsonValueKind.String &&
                    nextRetryAtProperty.TryGetDateTimeOffset(out var parsedNextRetryAt)
                        ? parsedNextRetryAt
                        : (DateTimeOffset?)null)
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .OrderBy(value => value)
                .FirstOrDefault();

            return nextRetry == default
                ? "not scheduled"
                : nextRetry.ToString("O");
        }
        catch (JsonException)
        {
            return "not scheduled";
        }
    }

    private static string DescribePendingGithubSyncLastAttempt(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not attempted";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("pendingGithubSync", out var pendingGithubSync) ||
                pendingGithubSync.ValueKind != JsonValueKind.Array)
            {
                return "not attempted";
            }

            var lastAttempt = pendingGithubSync
                .EnumerateArray()
                .Select(item =>
                    item.TryGetProperty("lastAttemptedAt", out var lastAttemptedAtProperty) &&
                    lastAttemptedAtProperty.ValueKind == JsonValueKind.String &&
                    lastAttemptedAtProperty.TryGetDateTimeOffset(out var parsedLastAttemptedAt)
                        ? parsedLastAttemptedAt
                        : (DateTimeOffset?)null)
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .OrderByDescending(value => value)
                .FirstOrDefault();

            return lastAttempt == default
                ? "not attempted"
                : lastAttempt.ToString("O");
        }
        catch (JsonException)
        {
            return "not attempted";
        }
    }

    private static string DescribePendingGithubSyncAttempts(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));

            if (document.RootElement.TryGetProperty("pendingGithubSync", out var pendingGithubSync) &&
                pendingGithubSync.ValueKind == JsonValueKind.Array)
            {
                var attemptCounts = pendingGithubSync
                    .EnumerateArray()
                    .Select(item =>
                        item.TryGetProperty("attemptCount", out var attemptCountProperty) &&
                        attemptCountProperty.ValueKind == JsonValueKind.Number &&
                        attemptCountProperty.TryGetInt32(out var parsedAttemptCount)
                            ? parsedAttemptCount
                            : 0)
                    .Where(value => value > 0)
                    .ToArray();

                if (attemptCounts.Length > 0)
                {
                    return $"max {attemptCounts.Max()}";
                }
            }

            if (document.RootElement.TryGetProperty("pendingGithubSyncCount", out var countProperty) &&
                countProperty.ValueKind == JsonValueKind.Number &&
                countProperty.TryGetInt32(out var count) &&
                count == 0)
            {
                return "none";
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribePendingGithubSyncIssues(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "none";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("pendingGithubSync", out var pendingGithubSync) ||
                pendingGithubSync.ValueKind != JsonValueKind.Array)
            {
                return "none";
            }

            var issueNumbers = pendingGithubSync
                .EnumerateArray()
                .Select(item =>
                    item.TryGetProperty("issueNumber", out var issueNumberProperty) &&
                    issueNumberProperty.ValueKind == JsonValueKind.Number &&
                    issueNumberProperty.TryGetInt32(out var parsedIssueNumber)
                        ? parsedIssueNumber
                        : (int?)null)
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .Distinct()
                .OrderBy(value => value)
                .Select(value => $"#{value}")
                .ToArray();

            return issueNumbers.Length == 0
                ? "none"
                : string.Join(", ", issueNumbers);
        }
        catch (JsonException)
        {
            return "none";
        }
    }

    private static string DescribePendingGithubSyncOldestQueuedAt(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("pendingGithubSync", out var pendingGithubSync) ||
                pendingGithubSync.ValueKind != JsonValueKind.Array)
            {
                return "not recorded";
            }

            var oldestQueuedAt = pendingGithubSync
                .EnumerateArray()
                .Select(item =>
                    item.TryGetProperty("recordedAt", out var recordedAtProperty) &&
                    recordedAtProperty.ValueKind == JsonValueKind.String &&
                    recordedAtProperty.TryGetDateTimeOffset(out var parsedRecordedAt)
                        ? parsedRecordedAt
                        : (DateTimeOffset?)null)
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .OrderBy(value => value)
                .FirstOrDefault();

            return oldestQueuedAt == default
                ? "not recorded"
                : oldestQueuedAt.ToString("O");
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribePendingGithubSyncOldestAge(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("pendingGithubSync", out var pendingGithubSync) ||
                pendingGithubSync.ValueKind != JsonValueKind.Array)
            {
                return "not recorded";
            }

            var oldestQueuedAt = pendingGithubSync
                .EnumerateArray()
                .Select(item =>
                    item.TryGetProperty("recordedAt", out var recordedAtProperty) &&
                    recordedAtProperty.ValueKind == JsonValueKind.String &&
                    recordedAtProperty.TryGetDateTimeOffset(out var parsedRecordedAt)
                        ? parsedRecordedAt
                        : (DateTimeOffset?)null)
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .OrderBy(value => value)
                .FirstOrDefault();

            if (oldestQueuedAt == default)
            {
                return "not recorded";
            }

            var generatedAt = root.TryGetProperty("generatedAt", out var generatedAtProperty) &&
                generatedAtProperty.ValueKind == JsonValueKind.String &&
                generatedAtProperty.TryGetDateTimeOffset(out var parsedGeneratedAt)
                ? parsedGeneratedAt
                : (DateTimeOffset?)null;

            if (generatedAt is null)
            {
                return "not recorded";
            }

            return FormatElapsed(generatedAt.Value - oldestQueuedAt);
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalLatestPass(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("latestPass", out var latestPass) ||
                latestPass.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            var passNumber = latestPass.TryGetProperty("passNumber", out var passNumberProperty) &&
                passNumberProperty.ValueKind == JsonValueKind.Number &&
                passNumberProperty.TryGetInt32(out var parsedPassNumber)
                ? parsedPassNumber
                : 0;
            var cycleCount = latestPass.TryGetProperty("cycleCount", out var cycleCountProperty) &&
                cycleCountProperty.ValueKind == JsonValueKind.Number &&
                cycleCountProperty.TryGetInt32(out var parsedCycleCount)
                ? parsedCycleCount
                : 0;
            var seededCycles = latestPass.TryGetProperty("seededCycles", out var seededCyclesProperty) &&
                seededCyclesProperty.ValueKind == JsonValueKind.Number &&
                seededCyclesProperty.TryGetInt32(out var parsedSeededCycles)
                ? parsedSeededCycles
                : 0;
            var consumedCycles = latestPass.TryGetProperty("consumedCycles", out var consumedCyclesProperty) &&
                consumedCyclesProperty.ValueKind == JsonValueKind.Number &&
                consumedCyclesProperty.TryGetInt32(out var parsedConsumedCycles)
                ? parsedConsumedCycles
                : 0;
            var replayAttempted = latestPass.TryGetProperty("githubReplayAttemptedCount", out var replayAttemptedProperty) &&
                replayAttemptedProperty.ValueKind == JsonValueKind.Number &&
                replayAttemptedProperty.TryGetInt32(out var parsedReplayAttempted)
                ? parsedReplayAttempted
                : 0;
            var replayUpdated = latestPass.TryGetProperty("githubReplayUpdatedCount", out var replayUpdatedProperty) &&
                replayUpdatedProperty.ValueKind == JsonValueKind.Number &&
                replayUpdatedProperty.TryGetInt32(out var parsedReplayUpdated)
                ? parsedReplayUpdated
                : 0;
            var escalationQueued = latestPass.TryGetProperty("operatorEscalationQueuedCount", out var escalationQueuedProperty) &&
                escalationQueuedProperty.ValueKind == JsonValueKind.Number &&
                escalationQueuedProperty.TryGetInt32(out var parsedEscalationQueued)
                ? parsedEscalationQueued
                : 0;
            var escalationConsumed = latestPass.TryGetProperty("operatorEscalationConsumedCount", out var escalationConsumedProperty) &&
                escalationConsumedProperty.ValueKind == JsonValueKind.Number &&
                escalationConsumedProperty.TryGetInt32(out var parsedEscalationConsumed)
                ? parsedEscalationConsumed
                : 0;

            var passLabel = passNumber > 0
                ? $"pass {passNumber}"
                : "pass n/a";

            if (escalationQueued > 0 || escalationConsumed > 0)
            {
                return $"{passLabel}: {seededCycles} seed, {consumedCycles} consume, escalation {escalationConsumed}/{escalationQueued}";
            }

            if (replayAttempted > 0)
            {
                return $"{passLabel}: {seededCycles} seed, {consumedCycles} consume, replay {replayUpdated}/{replayAttempted}";
            }

            return $"{passLabel}: {cycleCount} cycle{(cycleCount == 1 ? string.Empty : "s")}, {seededCycles} seed, {consumedCycles} consume";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalLatestPassOutcome(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("latestPass", out var latestPass) ||
                latestPass.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            var reachedIdle = latestPass.TryGetProperty("reachedIdle", out var reachedIdleProperty) &&
                reachedIdleProperty.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                reachedIdleProperty.GetBoolean();
            var reachedMaxCycles = latestPass.TryGetProperty("reachedMaxCycles", out var reachedMaxCyclesProperty) &&
                reachedMaxCyclesProperty.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                reachedMaxCyclesProperty.GetBoolean();

            if (reachedIdle)
            {
                return "idle reached";
            }

            if (reachedMaxCycles)
            {
                return "pass cap reached";
            }

            return "active";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerAttention(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("attentionSummary", out var summaryProperty) &&
                summaryProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(summaryProperty.GetString()))
            {
                return summaryProperty.GetString()!;
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerLoopMode(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("recentLoopSignal", out var recentLoopSignal) &&
                recentLoopSignal.ValueKind == JsonValueKind.Object &&
                recentLoopSignal.TryGetProperty("mode", out var modeProperty) &&
                modeProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(modeProperty.GetString()))
            {
                return modeProperty.GetString()!;
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalWorkerLoopSummary(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("recentLoopSignal", out var recentLoopSignal) &&
                recentLoopSignal.ValueKind == JsonValueKind.Object &&
                recentLoopSignal.TryGetProperty("summary", out var summaryProperty) &&
                summaryProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(summaryProperty.GetString()))
            {
                return summaryProperty.GetString()!;
            }

            return "not recorded";
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static bool DescribeGlobalInterventionTargetAcknowledged(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            return document.RootElement.TryGetProperty("interventionTarget", out var interventionTarget) &&
                interventionTarget.ValueKind == JsonValueKind.Object &&
                interventionTarget.TryGetProperty("acknowledged", out var acknowledgedProperty) &&
                acknowledgedProperty.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                acknowledgedProperty.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string DescribeGlobalInterventionTarget(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "not recorded";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (!document.RootElement.TryGetProperty("interventionTarget", out var interventionTarget) ||
                interventionTarget.ValueKind != JsonValueKind.Object)
            {
                return "not recorded";
            }

            var kind = interventionTarget.TryGetProperty("kind", out var kindProperty) && kindProperty.ValueKind == JsonValueKind.String
                ? kindProperty.GetString()
                : null;
            var summary = interventionTarget.TryGetProperty("summary", out var summaryProperty) && summaryProperty.ValueKind == JsonValueKind.String
                ? summaryProperty.GetString()
                : null;
            var ageSummary = interventionTarget.TryGetProperty("ageSummary", out var ageSummaryProperty) && ageSummaryProperty.ValueKind == JsonValueKind.String
                ? ageSummaryProperty.GetString()
                : null;
            var escalation = interventionTarget.TryGetProperty("escalation", out var escalationProperty) && escalationProperty.ValueKind == JsonValueKind.String
                ? escalationProperty.GetString()
                : null;
            var acknowledged = interventionTarget.TryGetProperty("acknowledged", out var acknowledgedProperty) &&
                acknowledgedProperty.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? acknowledgedProperty.GetBoolean()
                : (bool?)null;
            var acknowledgedStreak = interventionTarget.TryGetProperty("acknowledgedStreak", out var acknowledgedStreakProperty) &&
                acknowledgedStreakProperty.ValueKind == JsonValueKind.Number &&
                acknowledgedStreakProperty.TryGetInt32(out var streak)
                ? streak
                : (int?)null;

            if (string.IsNullOrWhiteSpace(kind) && string.IsNullOrWhiteSpace(summary))
            {
                return "not recorded";
            }

            if (string.IsNullOrWhiteSpace(kind))
            {
                return summary ?? "not recorded";
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                return AppendInterventionTargetAge(kind, ageSummary, escalation, acknowledged, acknowledgedStreak);
            }

            return AppendInterventionTargetAge($"{kind}: {summary}", ageSummary, escalation, acknowledged, acknowledgedStreak);
        }
        catch (JsonException)
        {
            return "not recorded";
        }
    }

    private static string DescribeGlobalInterventionEscalation(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "none";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("interventionEscalationNote", out var noteProperty) &&
                noteProperty.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(noteProperty.GetString()))
            {
                return noteProperty.GetString()!;
            }

            return "none";
        }
        catch (JsonException)
        {
            return "none";
        }
    }

    private static string DescribeGlobalInterventionEscalationStreak(string rootDirectory)
    {
        var runtimeStatusPath = Path.Combine(rootDirectory, RuntimeStatusRelativePath);
        if (!File.Exists(runtimeStatusPath))
        {
            return "0";
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(runtimeStatusPath));
            if (document.RootElement.TryGetProperty("interventionEscalationStreak", out var streakProperty) &&
                streakProperty.ValueKind == JsonValueKind.Number &&
                streakProperty.TryGetInt32(out var streak))
            {
                return streak.ToString();
            }

            return "0";
        }
        catch (JsonException)
        {
            return "0";
        }
    }

    private static string AppendInterventionTargetAge(string value, string? ageSummary, string? escalation, bool? acknowledged, int? acknowledgedStreak)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(ageSummary))
        {
            details.Add(ageSummary);
        }

        if (!string.IsNullOrWhiteSpace(escalation))
        {
            details.Add(escalation);
        }

        if (acknowledged is true)
        {
            details.Add(acknowledgedStreak is > 0
                ? $"acknowledged x{acknowledgedStreak.Value}"
                : "acknowledged");
        }

        return details.Count == 0
            ? value
            : $"{value} ({string.Join(", ", details)})";
    }

    private static string FormatPendingGithubSyncAge(PendingGithubSyncSnapshot pending, DateTimeOffset referenceTime)
    {
        var elapsed = referenceTime >= pending.RecordedAt
            ? referenceTime - pending.RecordedAt
            : TimeSpan.Zero;
        return $"queued {FormatElapsed(elapsed)} ago";
    }

    private static IReadOnlyList<PendingGithubSyncSnapshot> ReadPendingGithubSync(string rootDirectory)
    {
        var statusPath = Path.Combine(rootDirectory, ".dragon", "status", "pending-github-sync.json");
        if (!File.Exists(statusPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PendingGithubSyncSnapshot>>(
                File.ReadAllText(statusPath),
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void MarkSupersededRecoveryIssues(
        string owner,
        string repo,
        IssueWorkflowState workflow,
        string rootDirectory)
    {
        if (!(workflow.ActiveRecoveryIssueNumbers?.Any() ?? false))
        {
            return;
        }

        var activeRecoveryIssueNumbers = workflow.ActiveRecoveryIssueNumbers!
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (activeRecoveryIssueNumbers.Length < 2)
        {
            return;
        }

        var latestRecoveryIssueNumber = activeRecoveryIssueNumbers[^1];
        EnsureLabel(owner, repo, "superseded", "6E7781", "Older overlapping work that has been replaced by a newer path.", rootDirectory);

        foreach (var issueNumber in activeRecoveryIssueNumbers[..^1])
        {
            var commentBody = string.Join(
                Environment.NewLine,
                [
                    SupersededMarker,
                    "Automated backend recovery update:",
                    "- recovery status: superseded",
                    $"- source issue: #{workflow.IssueNumber}",
                    $"- superseded by: #{latestRecoveryIssueNumber}",
                    "- reason: a newer unresolved recovery path now exists for the same parent issue",
                    "- next step: continue work on the newer recovery issue and keep this older path as history unless it is explicitly reactivated"
                ]
            );

            RemoveLabel(owner, repo, issueNumber, "in-progress", rootDirectory);
            RemoveLabel(owner, repo, issueNumber, "quarantined", rootDirectory);
            RemoveLabel(owner, repo, issueNumber, "stalled", rootDirectory);
            RemoveLabel(owner, repo, issueNumber, "validated", rootDirectory);
            RemoveLabel(owner, repo, issueNumber, "waiting-follow-up", rootDirectory);
            AddLabel(owner, repo, issueNumber, "superseded", rootDirectory);
            UpsertMarkedComment(owner, repo, issueNumber, SupersededMarker, commentBody, rootDirectory);
        }

        RemoveLabel(owner, repo, latestRecoveryIssueNumber, "superseded", rootDirectory);
    }

    private int? EnsureRecoveryIssue(
        string owner,
        string repo,
        IssueWorkflowState workflow,
        string currentStage,
        IReadOnlyList<ExecutionRecord> executionRecords,
        string rootDirectory)
    {
        if (workflow.ActiveRecoveryIssueNumbers?.Any() ?? false)
        {
            return workflow.ActiveRecoveryIssueNumbers!.Max();
        }

        var title = BuildRecoveryIssueTitle(workflow);
        var existingIssue = FindOpenIssueByTitle(owner, repo, title, rootDirectory);
        if (existingIssue is not null)
        {
            return existingIssue.Value;
        }

        var body = BuildRecoveryIssueBody(workflow, currentStage, executionRecords, rootDirectory);
        var createCommand =
            $"issue create --repo {owner}/{repo} --title \"{EscapeForDoubleQuotes(title)}\" --body \"{EscapeForDoubleQuotes(body)}\" --label story --label recovery --label backlog";
        string result;
        try
        {
            result = commandRunner(createCommand, rootDirectory);
        }
        catch (InvalidOperationException ex) when (IsMissingLabelFailure(ex, "recovery"))
        {
            result = commandRunner(
                $"issue create --repo {owner}/{repo} --title \"{EscapeForDoubleQuotes(title)}\" --body \"{EscapeForDoubleQuotes(body)}\" --label story --label backlog",
                rootDirectory
            );
        }

        return ParseIssueNumber(result);
    }

    private int? FindOpenIssueByTitle(string owner, string repo, string title, string rootDirectory)
    {
        var json = commandRunner(
            $"issue list --repo {owner}/{repo} --state open --limit 500 --json number,title,labels",
            rootDirectory
        );

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (!TryReadIssueNumber(entry, out var issueNumber) || !TryReadLabels(entry, out var labels))
            {
                continue;
            }

            var issueTitle = ReadTextProperty(entry, "title");
            if (!string.Equals(issueTitle, title, StringComparison.Ordinal))
            {
                continue;
            }

            if (labels.Contains("recovery", StringComparer.OrdinalIgnoreCase) ||
                InferSourceIssueNumber(issueTitle, string.Empty) is not null &&
                issueTitle.Contains("[Recovery]", StringComparison.OrdinalIgnoreCase))
            {
                return issueNumber;
            }
        }
        }

        return null;
    }

    private IReadOnlyList<int> RetireReleasedRecoveryIssues(
        string owner,
        string repo,
        IssueWorkflowState workflow,
        string rootDirectory)
    {
        if (workflow.SourceIssueNumber is not null)
        {
            return [];
        }

        var retiredIssueNumbers = new List<int>();
        foreach (var recoveryIssue in FindOpenRecoveryIssuesForSource(owner, repo, workflow.IssueNumber, rootDirectory))
        {
            RemoveLabel(owner, repo, recoveryIssue.Number, "in-progress", rootDirectory);
            RemoveLabel(owner, repo, recoveryIssue.Number, "quarantined", rootDirectory);
            RemoveLabel(owner, repo, recoveryIssue.Number, "stalled", rootDirectory);
            RemoveLabel(owner, repo, recoveryIssue.Number, "validated", rootDirectory);
            RemoveLabel(owner, repo, recoveryIssue.Number, "waiting-follow-up", rootDirectory);
            RemoveLabel(owner, repo, recoveryIssue.Number, "superseded", rootDirectory);
            commandRunner(
                $"issue close {recoveryIssue.Number} --repo {owner}/{repo} --comment \"{EscapeForDoubleQuotes(BuildRecoveryRetiredComment(workflow.IssueNumber))}\"",
                rootDirectory
            );
            retiredIssueNumbers.Add(recoveryIssue.Number);
        }

        return retiredIssueNumbers;
    }

    private IReadOnlyList<GithubIssue> FindOpenRecoveryIssuesForSource(string owner, string repo, int sourceIssueNumber, string rootDirectory)
    {
        var json = commandRunner(
            $"issue list --repo {owner}/{repo} --state open --limit 500 --json number,title,body,labels",
            rootDirectory
        );

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var issues = new List<GithubIssue>();
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (!TryReadIssueNumber(entry, out var issueNumber) || !TryReadLabels(entry, out var labels))
            {
                continue;
            }

            var title = ReadTextProperty(entry, "title");
            var body = ReadTextProperty(entry, "body");

            if (!labels.Contains("recovery", StringComparer.OrdinalIgnoreCase) &&
                !title.Contains("[Recovery]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (InferSourceIssueNumber(title, body) != sourceIssueNumber)
            {
                continue;
            }

            issues.Add(new GithubIssue(
                issueNumber,
                title,
                "OPEN",
                labels,
                body,
                SourceIssueNumber: sourceIssueNumber
            ));
        }

        return issues.OrderBy(issue => issue.Number).ToArray();
        }
    }

    private static string ReadTextProperty(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static bool TryReadIssueNumber(JsonElement entry, out int issueNumber)
    {
        issueNumber = 0;
        return entry.TryGetProperty("number", out var numberProperty) &&
            numberProperty.ValueKind == JsonValueKind.Number &&
            numberProperty.TryGetInt32(out issueNumber);
    }

    private static bool TryReadLabels(JsonElement entry, out string[] labels)
    {
        labels = [];
        if (!entry.TryGetProperty("labels", out var labelsProperty) || labelsProperty.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        labels = labelsProperty
            .EnumerateArray()
            .Where(label =>
                label.ValueKind == JsonValueKind.Object &&
                label.TryGetProperty("name", out var nameProperty) &&
                nameProperty.ValueKind == JsonValueKind.String)
            .Select(label => label.GetProperty("name").GetString())
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return true;
    }

    private static string BuildRecoveryRetiredComment(int sourceIssueNumber) =>
        string.Join(
            Environment.NewLine,
            [
                RecoveryRetiredMarker,
                "Automated backend recovery update:",
                "- recovery status: retired",
                $"- source issue: #{sourceIssueNumber}",
                "- reason: the parent issue has returned to active flow and this recovery path is no longer the active route forward"
            ]
        );

    private static string BuildRecoveryIssueTitle(IssueWorkflowState workflow) =>
        $"[Recovery] Issue #{workflow.IssueNumber}: {workflow.IssueTitle}";

    private static string BuildRecoveryIssueBody(
        IssueWorkflowState workflow,
        string currentStage,
        IReadOnlyList<ExecutionRecord> executionRecords,
        string rootDirectory)
    {
        var changedPaths = GetMeaningfulChangedPaths(executionRecords, rootDirectory);
        return string.Join(
            Environment.NewLine,
            [
                $"Recovery story for quarantined issue #{workflow.IssueNumber}.",
                "",
                "Context:",
                $"- source issue: #{workflow.IssueNumber}",
                $"- blocked stage: {currentStage}",
                $"- quarantine reason: {workflow.Note ?? "No note recorded."}",
                changedPaths.Count > 0 ? $"- changed paths: {string.Join(", ", changedPaths)}" : "- changed paths: none recorded",
                "",
                "Suggested recovery steps:",
                "- reproduce the blocked stage locally",
                "- narrow the failing scope to the smallest viable fix",
                "- update the source issue and close this recovery story once the path is clear"
            ]
        );
    }

    private static string StripRepoRootPrefix(string path, string rootDirectory)
    {
        var normalizedRoot = rootDirectory.Trim().Replace('\\', '/');
        var stripped = StripPrefix(path, normalizedRoot);
        if (!ReferenceEquals(stripped, path))
        {
            return stripped;
        }

        var windowsRoot = TryConvertWslPathToWindows(normalizedRoot);
        if (windowsRoot is not null)
        {
            stripped = StripPrefix(path, windowsRoot);
            if (!ReferenceEquals(stripped, path))
            {
                return stripped;
            }
        }

        var wslRoot = TryConvertWindowsPathToWsl(normalizedRoot);
        return wslRoot is not null
            ? StripPrefix(path, wslRoot)
            : path;
    }

    private static string StripPrefix(string path, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return path;
        }

        if (path.Equals(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var prefixed = prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : $"{prefix}/";
        return path.StartsWith(prefixed, StringComparison.OrdinalIgnoreCase)
            ? path[prefixed.Length..]
            : path;
    }

    private static string? TryConvertWslPathToWindows(string normalizedRoot)
    {
        var match = Regex.Match(normalizedRoot, @"^/mnt/(?<drive>[a-zA-Z])/(?<rest>.+)$");
        if (!match.Success)
        {
            return null;
        }

        return $"{match.Groups["drive"].Value.ToUpperInvariant()}:/{match.Groups["rest"].Value}";
    }

    private static string? TryConvertWindowsPathToWsl(string normalizedRoot)
    {
        var match = Regex.Match(normalizedRoot, @"^(?<drive>[a-zA-Z]):/(?<rest>.+)$");
        if (!match.Success)
        {
            return null;
        }

        return $"/mnt/{match.Groups["drive"].Value.ToLowerInvariant()}/{match.Groups["rest"].Value}";
    }

    private static string NormalizeRelativePathSegments(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.Contains("..", StringComparison.Ordinal))
        {
            return path;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            if (string.Equals(segment, ".", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                if (normalized.Count == 0 || string.Equals(normalized[^1], "..", StringComparison.Ordinal))
                {
                    normalized.Add(segment);
                    continue;
                }

                normalized.RemoveAt(normalized.Count - 1);
                continue;
            }

            normalized.Add(segment);
        }

        return string.Join("/", normalized);
    }

    private static string StripQueryAndFragment(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        var queryIndex = path.IndexOf('?');
        var withoutQuery = queryIndex >= 0 ? path[..queryIndex] : path;

        var fragmentIndex = withoutQuery.IndexOf('#');
        if (fragmentIndex < 0)
        {
            return withoutQuery;
        }

        var fragment = withoutQuery[fragmentIndex..];
        return Regex.IsMatch(fragment, @"^#L\d+(?:C\d+)?$", RegexOptions.IgnoreCase)
            ? withoutQuery[..fragmentIndex]
            : withoutQuery;
    }

    private static string DecodeHtmlEntities(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.Contains('&', StringComparison.Ordinal))
        {
            return path;
        }

        return System.Net.WebUtility.HtmlDecode(path);
    }

    private static string UnwrapMarkdownPathReference(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length < 2)
        {
            return path;
        }

        var markdownLinkMatch = Regex.Match(path, @"^\[[^\]]*\]\((?<path>.+)\)$");
        if (markdownLinkMatch.Success)
        {
            var linkTarget = markdownLinkMatch.Groups["path"].Value.Trim();
            var strippedLinkTarget = StripQueryAndFragment(linkTarget);
            var titleMatch = Regex.Match(strippedLinkTarget, "^(?<path>.+?)\\s+(?:\"[^\"]*\"|'[^']*'|\\([^)]*\\))$");
            var normalizedLinkTarget = titleMatch.Success
                ? titleMatch.Groups["path"].Value.Trim()
                : strippedLinkTarget;
            return UnwrapSimplePathWrapper(normalizedLinkTarget);
        }

        return UnwrapSimplePathWrapper(path);
    }

    private static string UnwrapSimplePathWrapper(string path)
    {
        var current = path;

        while (!string.IsNullOrWhiteSpace(current) && current.Length >= 2)
        {
            var unwrapped =
                (current[0], current[^1]) is ('`', '`') or ('[', ']') or ('(', ')') or ('"', '"') or ('\'', '\'')
                    ? current[1..^1].Trim()
                    : current[0] == '<' && current[^1] == '>'
                    ? current[1..^1].Trim()
                    : current;

            if (ReferenceEquals(unwrapped, current) || string.Equals(unwrapped, current, StringComparison.Ordinal))
            {
                return current;
            }

            current = unwrapped;
        }

        return current;
    }

    private static string DecodePercentEncoding(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.Contains('%', StringComparison.Ordinal))
        {
            return path;
        }

        try
        {
            return Uri.UnescapeDataString(path);
        }
        catch (UriFormatException)
        {
            return path;
        }
    }

    private static bool IsRepoRelativeChangedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (Regex.IsMatch(path, "^[a-zA-Z]:/"))
        {
            return false;
        }

        if (Regex.IsMatch(path, "^[a-zA-Z][a-zA-Z0-9+.-]*:"))
        {
            return false;
        }

        return !string.Equals(path, "..", StringComparison.Ordinal) &&
            !path.StartsWith("../", StringComparison.Ordinal);
    }

    private static int? ParseIssueNumber(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        var match = Regex.Match(result, @"/issues/(?<number>\d+)");
        if (!match.Success)
        {
            return null;
        }

        return int.TryParse(
            match.Groups["number"].Value,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out var issueNumber)
            ? issueNumber
            : null;
    }

    private static int? InferSourceIssueNumber(string title, string body)
    {
        // Prefer the canonical recovery-title reference when both title and body mention a source issue.
        var titleNumber = ExtractFirstIssueNumber(title, @"\[Recovery\]\s+Issue\s+#(?<number>\d+)");
        if (titleNumber is not null)
        {
            return titleNumber.Value;
        }

        // When the body mentions multiple source issues, keep the earliest parseable one;
        // repeated duplicates should collapse naturally to that same first valid reference.
        return ExtractFirstIssueNumber(body, @"source issue:\s+#(?<number>\d+)");
    }

    private static int? ExtractFirstIssueNumber(string value, string pattern)
    {
        foreach (Match match in Regex.Matches(value, pattern, RegexOptions.IgnoreCase))
        {
            if (int.TryParse(match.Groups["number"].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }
        }

        return null;
    }

    private void EnsureLabel(string owner, string repo, string name, string color, string description, string rootDirectory)
    {
        try
        {
            commandRunner(
                $"label create {name} --repo {owner}/{repo} --color {color} --description \"{EscapeForDoubleQuotes(description)}\"",
                rootDirectory
            );
        }
        catch (InvalidOperationException)
        {
            // The label may already exist, which is fine for our sync path.
        }
    }

    private void AddLabel(string owner, string repo, int issueNumber, string name, string rootDirectory)
    {
        try
        {
            commandRunner(
                $"issue edit {issueNumber} --repo {owner}/{repo} --add-label {name}",
                rootDirectory
            );
        }
        catch (InvalidOperationException ex) when (IsLabelMutationPermissionFailure(ex))
        {
            // Some fine-grained tokens can read and comment on issues but not mutate labels.
            // Keep the workflow moving even if repo label hygiene cannot be enforced.
        }
    }

    private void RemoveLabel(string owner, string repo, int issueNumber, string name, string rootDirectory)
    {
        try
        {
            commandRunner(
                $"issue edit {issueNumber} --repo {owner}/{repo} --remove-label {name}",
                rootDirectory
            );
        }
        catch (InvalidOperationException)
        {
            // It's fine if the label is not currently applied.
        }
    }

    private static bool IsMissingLabelFailure(InvalidOperationException exception, string labelName)
    {
        var message = exception.Message;
        return message.Contains("label", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("not found", StringComparison.OrdinalIgnoreCase) &&
            message.Contains(labelName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLabelMutationPermissionFailure(InvalidOperationException exception)
    {
        var message = exception.Message;
        return message.Contains("Resource not accessible by personal access token", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("addLabelsToLabelable", StringComparison.OrdinalIgnoreCase);
    }

    private void UpsertHeartbeatComment(string owner, string repo, int issueNumber, string body, string rootDirectory)
    {
        UpsertMarkedComment(owner, repo, issueNumber, HeartbeatMarker, body, rootDirectory);
    }

    private void UpsertMarkedComment(string owner, string repo, int issueNumber, string marker, string body, string rootDirectory)
    {
        var existingCommentId = FindMarkedCommentId(owner, repo, issueNumber, marker, rootDirectory);
        if (existingCommentId is null)
        {
            commandRunner(
                $"api repos/{owner}/{repo}/issues/{issueNumber}/comments --method POST -f body=\"{EscapeForDoubleQuotes(body)}\"",
                rootDirectory
            );
            return;
        }

        commandRunner(
            $"api repos/{owner}/{repo}/issues/comments/{existingCommentId} --method PATCH -f body=\"{EscapeForDoubleQuotes(body)}\"",
            rootDirectory
        );
    }

    private long? FindMarkedCommentId(string owner, string repo, int issueNumber, string marker, string rootDirectory)
    {
        var json = commandRunner(
            $"api repos/{owner}/{repo}/issues/{issueNumber}/comments",
            rootDirectory
        );

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("id", out var idProperty) || idProperty.ValueKind != JsonValueKind.Number || !idProperty.TryGetInt64(out var commentId))
            {
                continue;
            }

            var body = ReadTextProperty(entry, "body");
            if (body.Contains(marker, StringComparison.Ordinal))
            {
                return commentId;
            }
        }
        }

        return null;
    }

    private static string EscapeForDoubleQuotes(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}

public sealed record StallState(
    bool IsStalled,
    TimeSpan Elapsed
);
