using System.Text.Json;

namespace Dragon.Backend.Orchestrator;

public sealed class MonitoringFindingStore
{
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly Func<DateTimeOffset> nowProvider;

    public MonitoringFindingStore(string rootDirectory, Func<DateTimeOffset>? nowProvider = null)
    {
        RootDirectory = rootDirectory;
        this.nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public string RootDirectory { get; }

    public string MonitoringDirectory => Path.Combine(RootDirectory, ".dragon", "monitoring");

    public string FindingsPath => Path.Combine(MonitoringDirectory, "continuous-monitoring.json");

    public MonitoringFinding Upsert(
        string category,
        string severity,
        string status,
        string project,
        int? issueNumber,
        string summary,
        string recommendation,
        bool triggerAutomatedUpdate)
    {
        Directory.CreateDirectory(MonitoringDirectory);
        var findings = ReadAll().ToList();
        var normalizedCategory = Normalize(category);
        var normalizedProject = Normalize(project);
        var normalizedSummary = Normalize(summary);
        var now = nowProvider();

        var existingIndex = findings.FindIndex(finding =>
            string.Equals(Normalize(finding.Category), normalizedCategory, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Normalize(finding.Project), normalizedProject, StringComparison.OrdinalIgnoreCase) &&
            finding.IssueNumber == issueNumber &&
            string.Equals(Normalize(finding.Summary), normalizedSummary, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            var existing = findings[existingIndex];
            var updated = existing with
            {
                Severity = severity,
                Status = status,
                Recommendation = recommendation,
                TriggerAutomatedUpdate = triggerAutomatedUpdate,
                LastObservedAt = now
            };
            findings[existingIndex] = updated;
            WriteAll(findings);
            return updated;
        }

        var finding = new MonitoringFinding(
            Guid.NewGuid().ToString("N"),
            category,
            severity,
            status,
            project,
            issueNumber,
            summary,
            recommendation,
            triggerAutomatedUpdate,
            now,
            now);
        findings.Add(finding);
        WriteAll(findings);
        return finding;
    }

    public IReadOnlyList<MonitoringFinding> ReadAll()
    {
        if (!File.Exists(FindingsPath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<MonitoringFinding>>(File.ReadAllText(FindingsPath), serializerOptions) ?? [];
    }

    private void WriteAll(IReadOnlyList<MonitoringFinding> findings)
    {
        File.WriteAllText(FindingsPath, JsonSerializer.Serialize(findings, serializerOptions));
    }

    private static string Normalize(string value) => value.Trim();
}

public sealed record MonitoringFinding(
    string Id,
    string Category,
    string Severity,
    string Status,
    string Project,
    int? IssueNumber,
    string Summary,
    string Recommendation,
    bool TriggerAutomatedUpdate,
    DateTimeOffset RecordedAt,
    DateTimeOffset LastObservedAt
);
