using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class QueueStore
{
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public QueueStore(string rootDirectory, string queueName = "dragon.jobs")
    {
        RootDirectory = rootDirectory;
        QueueName = queueName;
    }

    public string RootDirectory { get; }

    public string QueueName { get; }

    public string QueuePath => Path.Combine(RootDirectory, ".dragon", "queues", $"{QueueName}.ndjson");

    public void Enqueue(SelfBuildJob job)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
        File.AppendAllText(QueuePath, JsonSerializer.Serialize(job, serializerOptions) + Environment.NewLine);
    }

    public IReadOnlyList<SelfBuildJob> ReadAll()
    {
        if (!File.Exists(QueuePath))
        {
            return [];
        }

        return File.ReadAllLines(QueuePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<SelfBuildJob>(line, serializerOptions)!)
            .ToArray();
    }

    public SelfBuildJob? Dequeue()
    {
        var jobs = ReadAll().ToList();
        if (jobs.Count == 0)
        {
            return null;
        }

        var selectedIndex = jobs
            .Select((job, index) => new { job, index })
            .OrderBy(item => GetQueuePriorityRank(item.job))
            .ThenBy(item => GetTargetingRank(item.job))
            .ThenBy(item => GetRoleAlignmentRank(item.job))
            .ThenBy(item => GetActionRank(item.job))
            .ThenBy(item => item.index)
            .First()
            .index;

        var next = jobs[selectedIndex];
        jobs.RemoveAt(selectedIndex);

        if (jobs.Count == 0)
        {
            if (File.Exists(QueuePath))
            {
                File.Delete(QueuePath);
            }
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
            File.WriteAllLines(QueuePath, jobs.Select(job => JsonSerializer.Serialize(job, serializerOptions)));
        }

        return next;
    }

    private static int GetQueuePriorityRank(SelfBuildJob job)
    {
        if (string.Equals(job.Agent, "review", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.Agent, "test", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(job.Metadata.GetValueOrDefault("requestedBlocking"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(job.Metadata.GetValueOrDefault("requestedPriority"), "high", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (string.Equals(job.Metadata.GetValueOrDefault("requestedPriority"), "low", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return 3;
    }

    private static int GetTargetingRank(SelfBuildJob job)
    {
        var hasTargetArtifact = !string.IsNullOrWhiteSpace(job.Metadata.GetValueOrDefault("targetArtifact"));
        var hasTargetOutcome = !string.IsNullOrWhiteSpace(job.Metadata.GetValueOrDefault("targetOutcome"));

        if (hasTargetArtifact && hasTargetOutcome)
        {
            return 0;
        }

        if (hasTargetArtifact || hasTargetOutcome)
        {
            return 1;
        }

        return 2;
    }

    private static int GetRoleAlignmentRank(SelfBuildJob job)
    {
        var alignmentAgent = job.Metadata.GetValueOrDefault("preferredAgent") ?? job.Agent;
        return alignmentAgent.ToLowerInvariant() switch
        {
            var agent when IsDocumentationArtifact(job) && string.Equals(agent, "documentation", StringComparison.OrdinalIgnoreCase) => 0,
            var agent when IsDocumentationArtifact(job) && string.Equals(agent, "feedback", StringComparison.OrdinalIgnoreCase) => 1,
            var agent when IsCodeArtifact(job) && string.Equals(agent, "refactor", StringComparison.OrdinalIgnoreCase) => 0,
            var agent when IsCodeArtifact(job) && string.Equals(agent, "documentation", StringComparison.OrdinalIgnoreCase) => 1,
            "documentation" => 0,
            "refactor" => 1,
            "feedback" => 2,
            _ => 3
        };
    }

    private static bool IsDocumentationArtifact(SelfBuildJob job)
    {
        var artifact = job.Metadata.GetValueOrDefault("targetArtifact");
        return !string.IsNullOrWhiteSpace(artifact) &&
            (artifact.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) ||
             artifact.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCodeArtifact(SelfBuildJob job)
    {
        var artifact = job.Metadata.GetValueOrDefault("targetArtifact");
        return !string.IsNullOrWhiteSpace(artifact) &&
            (artifact.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) ||
             artifact.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
             artifact.EndsWith(".js", StringComparison.OrdinalIgnoreCase));
    }

    private static int GetActionRank(SelfBuildJob job) => job.Action.ToLowerInvariant() switch
    {
        "implement_issue" => 0,
        "summarize_issue" => 1,
        _ => 2
    };

    public int RemoveAll(Func<SelfBuildJob, bool> predicate)
    {
        var jobs = ReadAll().ToList();
        var remaining = jobs.Where(job => !predicate(job)).ToList();
        var removed = jobs.Count - remaining.Count;

        if (removed == 0)
        {
            return 0;
        }

        if (remaining.Count == 0)
        {
            if (File.Exists(QueuePath))
            {
                File.Delete(QueuePath);
            }
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(QueuePath)!);
            File.WriteAllLines(QueuePath, remaining.Select(job => JsonSerializer.Serialize(job, serializerOptions)));
        }

        return removed;
    }
}
