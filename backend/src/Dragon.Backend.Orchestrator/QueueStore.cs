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

        if (string.Equals(job.Metadata.GetValueOrDefault("requestedPriority"), "high", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(job.Metadata.GetValueOrDefault("requestedPriority"), "low", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return 2;
    }

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
