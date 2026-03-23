using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class WorkflowStateStore
{
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public WorkflowStateStore(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }

    public string StatePath => Path.Combine(RootDirectory, ".dragon", "state", "issues.json");

    public IReadOnlyDictionary<int, IssueWorkflowState> ReadAll()
    {
        if (!File.Exists(StatePath))
        {
            return new Dictionary<int, IssueWorkflowState>();
        }

        var json = File.ReadAllText(StatePath);
        var snapshots = JsonSerializer.Deserialize<List<IssueWorkflowState>>(json, serializerOptions) ?? [];
        return snapshots.ToDictionary(item => item.IssueNumber);
    }

    public IssueWorkflowState Update(SelfBuildJob job, JobExecutionResult execution)
    {
        var sourceIssueNumber = job.Metadata.TryGetValue("sourceIssueNumber", out var sourceIssueText) &&
            int.TryParse(sourceIssueText, out var sourceIssueNumberValue)
            ? sourceIssueNumberValue
            : (int?)null;

        return Update(job.Issue, job.Payload.Title, execution.Agent, execution, sourceIssueNumber);
    }

    public IssueWorkflowState Update(int issueNumber, string issueTitle, string agent, JobExecutionResult execution)
    {
        return Update(issueNumber, issueTitle, agent, execution, null);
    }

    public IssueWorkflowState Update(int issueNumber, string issueTitle, string agent, JobExecutionResult execution, int? sourceIssueNumber)
    {
        var snapshots = ReadAll().ToDictionary(entry => entry.Key, entry => entry.Value);
        snapshots.TryGetValue(issueNumber, out var existing);

        var stages = existing?.Stages.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, WorkflowStageState>(StringComparer.OrdinalIgnoreCase);

        stages[agent] = new WorkflowStageState(
            execution.Status,
            execution.JobId,
            execution.ObservedAt,
            execution.Summary
        );

        var overallStatus = DetermineOverallStatus(stages);
        var updated = new IssueWorkflowState(
            issueNumber,
            issueTitle,
            overallStatus,
            stages,
            DateTimeOffset.UtcNow,
            existing?.Note,
            sourceIssueNumber ?? existing?.SourceIssueNumber,
            existing?.ActiveRecoveryIssueNumbers ?? []
        );

        snapshots[issueNumber] = updated;
        ReconcileRecoveryLinkage(snapshots, issueNumber);

        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(
            StatePath,
            JsonSerializer.Serialize(snapshots.Values.OrderBy(item => item.IssueNumber).ToArray(), serializerOptions)
        );

        return updated;
    }

    public IssueWorkflowState OverrideOverallStatus(int issueNumber, string status, string note)
    {
        var snapshots = ReadAll().ToDictionary(entry => entry.Key, entry => entry.Value);
        if (!snapshots.TryGetValue(issueNumber, out var existing))
        {
            throw new InvalidOperationException($"Cannot override workflow state for unknown issue #{issueNumber}.");
        }

        var updated = existing with
        {
            OverallStatus = status,
            UpdatedAt = DateTimeOffset.UtcNow,
            Note = note
        };

        snapshots[existing.IssueNumber] = updated;
        ReconcileRecoveryLinkage(snapshots, issueNumber);
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(
            StatePath,
            JsonSerializer.Serialize(snapshots.Values.OrderBy(item => item.IssueNumber).ToArray(), serializerOptions)
        );

        return updated;
    }

    public IssueWorkflowState UpdateNote(int issueNumber, string note)
    {
        var snapshots = ReadAll().ToDictionary(entry => entry.Key, entry => entry.Value);
        if (!snapshots.TryGetValue(issueNumber, out var existing))
        {
            throw new InvalidOperationException($"Cannot update note for unknown issue #{issueNumber}.");
        }

        var updated = existing with
        {
            UpdatedAt = DateTimeOffset.UtcNow,
            Note = note
        };

        snapshots[existing.IssueNumber] = updated;
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(
            StatePath,
            JsonSerializer.Serialize(snapshots.Values.OrderBy(item => item.IssueNumber).ToArray(), serializerOptions)
        );

        return updated;
    }

    public IssueWorkflowState ReleaseQuarantineForRetry(int issueNumber, string note)
    {
        var snapshots = ReadAll().ToDictionary(entry => entry.Key, entry => entry.Value);
        if (!snapshots.TryGetValue(issueNumber, out var existing))
        {
            throw new InvalidOperationException($"Cannot release workflow state for unknown issue #{issueNumber}.");
        }

        if (existing.ActiveRecoveryIssueNumbers?.Any() == true)
        {
            throw new InvalidOperationException($"Cannot release issue #{issueNumber} while recovery children are still active.");
        }

        var retryStage = FailurePolicy.InferCurrentStage(existing);
        return ResetStageForRetryInternal(snapshots, existing, retryStage, note);
    }

    public IssueWorkflowState ResetStageForRetry(int issueNumber, string stage, string note)
    {
        var snapshots = ReadAll().ToDictionary(entry => entry.Key, entry => entry.Value);
        if (!snapshots.TryGetValue(issueNumber, out var existing))
        {
            throw new InvalidOperationException($"Cannot reset workflow stage for unknown issue #{issueNumber}.");
        }

        return ResetStageForRetryInternal(snapshots, existing, stage, note);
    }

    private IssueWorkflowState ResetStageForRetryInternal(
        IDictionary<int, IssueWorkflowState> snapshots,
        IssueWorkflowState existing,
        string? retryStage,
        string note)
    {
        var stages = existing.Stages.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(retryStage) &&
            stages.TryGetValue(retryStage, out var retryStageState) &&
            string.Equals(retryStageState.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            stages.Remove(retryStage);
        }

        var updated = existing with
        {
            Stages = stages,
            OverallStatus = DetermineOverallStatus(stages),
            UpdatedAt = DateTimeOffset.UtcNow,
            Note = note
        };

        snapshots[existing.IssueNumber] = updated;
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(
            StatePath,
            JsonSerializer.Serialize(snapshots.Values.OrderBy(item => item.IssueNumber).ToArray(), serializerOptions)
        );

        return updated;
    }

    private static void ReconcileRecoveryLinkage(IDictionary<int, IssueWorkflowState> snapshots, int issueNumber)
    {
        if (!snapshots.TryGetValue(issueNumber, out var workflow) || workflow.SourceIssueNumber is null)
        {
            return;
        }

        if (!snapshots.TryGetValue(workflow.SourceIssueNumber.Value, out var parent))
        {
            return;
        }

        var activeRecoveryIssueNumbers = (parent.ActiveRecoveryIssueNumbers ?? [])
            .ToHashSet();

        if (string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase))
        {
            activeRecoveryIssueNumbers.Remove(issueNumber);
        }
        else
        {
            activeRecoveryIssueNumbers.Add(issueNumber);
        }

        snapshots[parent.IssueNumber] = parent with
        {
            ActiveRecoveryIssueNumbers = activeRecoveryIssueNumbers.OrderBy(value => value).ToArray(),
            UpdatedAt = DateTimeOffset.UtcNow,
            OverallStatus = activeRecoveryIssueNumbers.Count == 0 &&
                string.Equals(parent.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase)
                ? "in_progress"
                : parent.OverallStatus,
            Note = activeRecoveryIssueNumbers.Count == 0 &&
                string.Equals(parent.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase)
                ? "Recovery child completed; parent returned to active flow."
                : parent.Note
        };
    }

    private static string DetermineOverallStatus(IReadOnlyDictionary<string, WorkflowStageState> stages)
    {
        if (stages.Values.Any(stage => string.Equals(stage.Status, "failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "failed";
        }

        var hasSuccessfulImplementationStage = stages.Any(stage =>
            !string.Equals(stage.Key, "review", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(stage.Key, "test", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(stage.Value.Status, "success", StringComparison.OrdinalIgnoreCase));
        var reviewSucceeded = stages.TryGetValue("review", out var reviewValue) &&
            string.Equals(reviewValue.Status, "success", StringComparison.OrdinalIgnoreCase);
        var testSucceeded = stages.TryGetValue("test", out var testValue) &&
            string.Equals(testValue.Status, "success", StringComparison.OrdinalIgnoreCase);

        if (hasSuccessfulImplementationStage && reviewSucceeded && testSucceeded)
        {
            return "validated";
        }

        return "in_progress";
    }
}
