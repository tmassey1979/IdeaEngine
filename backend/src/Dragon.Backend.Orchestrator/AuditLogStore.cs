using System.Text.Json;

namespace Dragon.Backend.Orchestrator;

public sealed class AuditLogStore
{
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly Func<DateTimeOffset> nowProvider;

    public AuditLogStore(string rootDirectory, Func<DateTimeOffset>? nowProvider = null)
    {
        RootDirectory = rootDirectory;
        this.nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public string RootDirectory { get; }

    public string AuditDirectory => Path.Combine(RootDirectory, ".dragon", "audit");

    public string AuditPath => Path.Combine(AuditDirectory, "audit-log.json");

    public AuditLogEntry Append(
        string actor,
        string action,
        string project,
        int? issueNumber,
        string details,
        string? source = null)
    {
        Directory.CreateDirectory(AuditDirectory);
        var entries = ReadAll().ToList();
        var entry = new AuditLogEntry(
            Guid.NewGuid().ToString("N"),
            actor,
            action,
            project,
            issueNumber,
            details,
            source,
            nowProvider());

        entries.Add(entry);
        File.WriteAllText(AuditPath, JsonSerializer.Serialize(entries, serializerOptions));
        return entry;
    }

    public IReadOnlyList<AuditLogEntry> ReadAll()
    {
        if (!File.Exists(AuditPath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<AuditLogEntry>>(File.ReadAllText(AuditPath), serializerOptions) ?? [];
    }
}

public sealed record AuditLogEntry(
    string Id,
    string Actor,
    string Action,
    string Project,
    int? IssueNumber,
    string Details,
    string? Source,
    DateTimeOffset RecordedAt
);
