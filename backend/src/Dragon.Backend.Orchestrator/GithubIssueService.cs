using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class GithubIssueService
{
    private const string HeartbeatMarker = "<!-- dragon-backend-heartbeat -->";
    private const string RemediationMarker = "<!-- dragon-backend-remediation -->";
    private static readonly TimeSpan StalledThreshold = TimeSpan.FromMinutes(15);
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
        using var document = JsonDocument.Parse(json);
        var issues = new List<GithubIssue>();

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var labels = entry.GetProperty("labels")
                .EnumerateArray()
                .Select(label => label.GetProperty("name").GetString())
                .OfType<string>()
                .ToArray();

            if (!labels.Contains("story", StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var title = entry.GetProperty("title").GetString() ?? string.Empty;
            backlogIndex.TryGetValue(title, out var metadata);

            issues.Add(new GithubIssue(
                entry.GetProperty("number").GetInt32(),
                title,
                entry.GetProperty("state").GetString() ?? "OPEN",
                labels,
                entry.GetProperty("body").GetString() ?? string.Empty,
                metadata?.Heading,
                metadata?.SourceFile
            ));
        }

        return issues.OrderBy(issue => issue.Number).ToArray();
    }

    public GithubSyncResult SyncWorkflow(
        string owner,
        string repo,
        IssueWorkflowState workflow,
        IReadOnlyList<ExecutionRecord> executionRecords,
        string rootDirectory)
    {
        if (string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
        {
            return SyncQuarantinedWorkflow(owner, repo, workflow, executionRecords, rootDirectory);
        }

        if (!string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase))
        {
            return SyncHeartbeatWorkflow(owner, repo, workflow, executionRecords, rootDirectory);
        }

        var commentBody = string.Join(
            Environment.NewLine,
            [
                "Automated backend sync update:",
                $"- workflow status: {workflow.OverallStatus}",
                $"- stages: {string.Join(", ", workflow.Stages.Select(stage => $"{stage.Key}={stage.Value.Status}"))}",
                executionRecords.Count > 0
                    ? $"- recent executions: {string.Join("; ", executionRecords.OrderByDescending(record => record.RecordedAt).Take(3).Reverse().Select(record => $"{record.JobAgent}:{record.Status}:{record.JobId}"))}"
                    : "- recent executions: none recorded",
                executionRecords.SelectMany(record => record.ChangedPaths).Distinct().Any()
                    ? $"- changed paths: {string.Join(", ", executionRecords.SelectMany(record => record.ChangedPaths).Distinct())}"
                    : "- changed paths: none recorded"
            ]
        );

        EnsureLabel(owner, repo, "in-progress", "F9D0C4", "Actively being implemented.", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "quarantined", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "in-progress", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "stalled", rootDirectory);
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
        var latestExecutionTiming = latestExecution is not null
            ? $"{latestExecution.RecordedAt:O} ({FormatElapsed(workflow.UpdatedAt - latestExecution.RecordedAt)} ago)"
            : null;
        var commentBody = string.Join(
            Environment.NewLine,
            [
                HeartbeatMarker,
                "Automated backend heartbeat:",
                $"- workflow status: {workflow.OverallStatus}",
                $"- current stage: {currentStage}",
                $"- current stage updated: {currentStageTiming}",
                $"- stalled: {(stallState.IsStalled ? "yes" : "no")}",
                stallState.IsStalled
                    ? $"- stalled reason: current stage has been idle for {FormatElapsed(stallState.Elapsed)}"
                    : "- stalled reason: none",
                latestExecution is not null
                    ? $"- latest outcome: {latestExecution.JobAgent} {latestExecution.Status} ({latestExecution.Summary})"
                    : "- latest outcome: none recorded",
                latestExecutionTiming is not null
                    ? $"- latest execution recorded: {latestExecutionTiming}"
                    : "- latest execution recorded: none",
                $"- stages: {string.Join(", ", workflow.Stages.Select(stage => $"{stage.Key}={stage.Value.Status}"))}",
                executionRecords.Count > 0
                    ? $"- recent executions: {string.Join("; ", executionRecords.OrderByDescending(record => record.RecordedAt).Take(3).Reverse().Select(record => $"{record.JobAgent}:{record.Status}:{record.JobId}"))}"
                    : "- recent executions: none recorded",
                workflow.Note is not null ? $"- note: {workflow.Note}" : "- note: none"
            ]
        );

        EnsureLabel(owner, repo, "in-progress", "F9D0C4", "Actively being implemented.", rootDirectory);
        EnsureLabel(owner, repo, "stalled", "C2A000", "In-progress work that appears stalled.", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "quarantined", rootDirectory);
        AddLabel(owner, repo, workflow.IssueNumber, "in-progress", rootDirectory);
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
        var currentStage = InferCurrentStage(workflow);
        var recoveryIssueNumber = EnsureRecoveryIssue(owner, repo, workflow, currentStage, executionRecords, rootDirectory);
        var commentBody = string.Join(
            Environment.NewLine,
            [
                RemediationMarker,
                "Automated backend quarantine update:",
                $"- workflow status: {workflow.OverallStatus}",
                $"- blocked stage: {currentStage}",
                $"- note: {workflow.Note ?? "No note recorded."}",
                recoveryIssueNumber is not null ? $"- recovery issue: #{recoveryIssueNumber}" : "- recovery issue: not created",
                executionRecords.Count > 0
                    ? $"- recent failures: {string.Join("; ", executionRecords.OrderByDescending(record => record.RecordedAt).Take(3).Reverse().Select(record => $"{record.JobAgent}:{record.Status}:{record.JobId}"))}"
                    : "- recent failures: none recorded",
                executionRecords.SelectMany(record => record.ChangedPaths).Distinct().Any()
                    ? $"- changed paths: {string.Join(", ", executionRecords.SelectMany(record => record.ChangedPaths).Distinct())}"
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
        RemoveLabel(owner, repo, workflow.IssueNumber, "in-progress", rootDirectory);
        RemoveLabel(owner, repo, workflow.IssueNumber, "stalled", rootDirectory);
        AddLabel(owner, repo, workflow.IssueNumber, "quarantined", rootDirectory);
        UpsertMarkedComment(owner, repo, workflow.IssueNumber, RemediationMarker, commentBody, rootDirectory);

        return new GithubSyncResult(true, true, $"Updated GitHub issue #{workflow.IssueNumber} with quarantine trace.");
    }

    private int? EnsureRecoveryIssue(
        string owner,
        string repo,
        IssueWorkflowState workflow,
        string currentStage,
        IReadOnlyList<ExecutionRecord> executionRecords,
        string rootDirectory)
    {
        var title = BuildRecoveryIssueTitle(workflow);
        var existingIssue = FindOpenIssueByTitle(owner, repo, title, rootDirectory);
        if (existingIssue is not null)
        {
            return existingIssue.Value;
        }

        var body = BuildRecoveryIssueBody(workflow, currentStage, executionRecords);
        var result = commandRunner(
            $"issue create --repo {owner}/{repo} --title \"{EscapeForDoubleQuotes(title)}\" --body \"{EscapeForDoubleQuotes(body)}\" --label story --label recovery --label backlog",
            rootDirectory
        );

        return ParseIssueNumber(result);
    }

    private int? FindOpenIssueByTitle(string owner, string repo, string title, string rootDirectory)
    {
        var json = commandRunner(
            $"issue list --repo {owner}/{repo} --state open --limit 500 --json number,title,labels",
            rootDirectory
        );

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var issueTitle = entry.TryGetProperty("title", out var titleProperty) ? titleProperty.GetString() ?? string.Empty : string.Empty;
            if (!string.Equals(issueTitle, title, StringComparison.Ordinal))
            {
                continue;
            }

            var labels = entry.GetProperty("labels")
                .EnumerateArray()
                .Select(label => label.GetProperty("name").GetString())
                .OfType<string>()
                .ToArray();
            if (labels.Contains("recovery", StringComparer.OrdinalIgnoreCase))
            {
                return entry.GetProperty("number").GetInt32();
            }
        }

        return null;
    }

    private static string BuildRecoveryIssueTitle(IssueWorkflowState workflow) =>
        $"[Recovery] Issue #{workflow.IssueNumber}: {workflow.IssueTitle}";

    private static string BuildRecoveryIssueBody(
        IssueWorkflowState workflow,
        string currentStage,
        IReadOnlyList<ExecutionRecord> executionRecords)
    {
        var changedPaths = executionRecords.SelectMany(record => record.ChangedPaths).Distinct().ToArray();
        return string.Join(
            Environment.NewLine,
            [
                $"Recovery story for quarantined issue #{workflow.IssueNumber}.",
                "",
                "Context:",
                $"- source issue: #{workflow.IssueNumber}",
                $"- blocked stage: {currentStage}",
                $"- quarantine reason: {workflow.Note ?? "No note recorded."}",
                changedPaths.Length > 0 ? $"- changed paths: {string.Join(", ", changedPaths)}" : "- changed paths: none recorded",
                "",
                "Suggested recovery steps:",
                "- reproduce the blocked stage locally",
                "- narrow the failing scope to the smallest viable fix",
                "- update the source issue and close this recovery story once the path is clear"
            ]
        );
    }

    private static int? ParseIssueNumber(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(result, @"/issues/(?<number>\d+)");
        if (!match.Success)
        {
            return null;
        }

        return int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture);
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
        commandRunner(
            $"issue edit {issueNumber} --repo {owner}/{repo} --add-label {name}",
            rootDirectory
        );
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

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var body = entry.TryGetProperty("body", out var bodyProperty) ? bodyProperty.GetString() ?? string.Empty : string.Empty;
            if (body.Contains(marker, StringComparison.Ordinal))
            {
                return entry.GetProperty("id").GetInt64();
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
