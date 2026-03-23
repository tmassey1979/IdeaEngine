using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class ExecutionRecordStore
{
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public ExecutionRecordStore(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }

    public string RunsDirectory => Path.Combine(RootDirectory, ".dragon", "runs");

    public string RecordPath(int issueNumber) => Path.Combine(RunsDirectory, $"issue-{issueNumber}.json");

    public ExecutionRecord Append(SelfBuildJob job, JobExecutionResult execution, IReadOnlyList<SelfBuildJob> followUps)
    {
        Directory.CreateDirectory(RunsDirectory);
        var records = Read(job.Issue).ToList();

        var record = new ExecutionRecord(
            job.Issue,
            job.Payload.Title,
            job.Agent,
            job.Action,
            execution.JobId,
            execution.Status,
            execution.Summary,
            execution.ObservedAt,
            execution.ChangedPaths?.Count > 0 ? execution.ChangedPaths : ReadChangedPaths(job),
            followUps.Select(item => item.Agent).ToArray(),
            BuildNotes(job)
        );

        records.Add(record);
        File.WriteAllText(RecordPath(job.Issue), JsonSerializer.Serialize(records, serializerOptions));
        return record;
    }

    public IReadOnlyList<ExecutionRecord> Read(int issueNumber)
    {
        var path = RecordPath(issueNumber);
        if (!File.Exists(path))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ExecutionRecord>>(File.ReadAllText(path), serializerOptions) ?? [];
    }

    private static IReadOnlyList<string> ReadChangedPaths(SelfBuildJob job)
    {
        if (!job.Metadata.TryGetValue("changedPaths", out var changedPathsRaw) || string.IsNullOrWhiteSpace(changedPathsRaw))
        {
            return [];
        }

        return changedPathsRaw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string BuildNotes(SelfBuildJob job)
    {
        var notes = new List<string>();

        if (job.Metadata.TryGetValue("implementationConflictResolution", out var implementationResolution) &&
            !string.IsNullOrWhiteSpace(implementationResolution))
        {
            var supersededImplementationIssues = job.Metadata.GetValueOrDefault("supersededImplementationIssues");
            notes.Add(string.IsNullOrWhiteSpace(supersededImplementationIssues)
                ? implementationResolution
                : $"{implementationResolution} Superseded implementation issues: {supersededImplementationIssues}.");
        }

        if (job.Metadata.TryGetValue("summaryConflictResolution", out var summaryResolution) &&
            !string.IsNullOrWhiteSpace(summaryResolution))
        {
            var supersededSummaryIssues = job.Metadata.GetValueOrDefault("supersededSummaryIssues");
            notes.Add(string.IsNullOrWhiteSpace(supersededSummaryIssues)
                ? summaryResolution
                : $"{summaryResolution} Superseded summary issues: {supersededSummaryIssues}.");
        }

        if (string.Equals(job.Metadata.GetValueOrDefault("interventionEscalation"), "true", StringComparison.OrdinalIgnoreCase))
        {
            var signature = job.Metadata.GetValueOrDefault("interventionSignature");
            if (!string.IsNullOrWhiteSpace(signature))
            {
                notes.Add($"Intervention escalation acknowledged: {signature}.");
            }
        }

        return string.Join(" ", notes);
    }
}
