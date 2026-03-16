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

    public IssueWorkflowState Update(int issueNumber, string issueTitle, string agent, JobExecutionResult execution)
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
            existing?.Note
        );

        snapshots[issueNumber] = updated;

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

        snapshots[issueNumber] = updated;
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(
            StatePath,
            JsonSerializer.Serialize(snapshots.Values.OrderBy(item => item.IssueNumber).ToArray(), serializerOptions)
        );

        return updated;
    }

    private static string DetermineOverallStatus(IReadOnlyDictionary<string, WorkflowStageState> stages)
    {
        if (stages.Values.Any(stage => string.Equals(stage.Status, "failed", StringComparison.OrdinalIgnoreCase)))
        {
            return "failed";
        }

        var required = new[] { "developer", "review", "test" };
        if (required.All(stage => stages.TryGetValue(stage, out var value) && string.Equals(value.Status, "success", StringComparison.OrdinalIgnoreCase)))
        {
            return "validated";
        }

        return "in_progress";
    }
}
