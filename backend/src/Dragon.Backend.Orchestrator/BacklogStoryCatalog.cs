using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static class BacklogStoryCatalog
{
    public static IReadOnlyList<GithubIssue> LoadStories(string rootDirectory)
    {
        var backlogPath = Path.Combine(rootDirectory, "planning", "backlog.json");

        if (!File.Exists(backlogPath))
        {
            throw new FileNotFoundException("Could not find planning/backlog.json.", backlogPath);
        }

        using var stream = File.OpenRead(backlogPath);
        using var document = JsonDocument.Parse(stream);

        var stories = new List<GithubIssue>();
        foreach (var story in document.RootElement.GetProperty("stories").EnumerateArray())
        {
            var title = story.GetProperty("title").GetString() ?? string.Empty;
            var heading = story.GetProperty("heading").GetString();
            var sourceFile = story.GetProperty("sourceFile").GetString();
            var technicalDetails = ReadTechnicalDetails(story);
            var labels = new[] { "story" };

            stories.Add(new GithubIssue(
                ParseIssueNumber(story.GetProperty("id").GetString()),
                title,
                "OPEN",
                labels,
                story.GetProperty("description").GetString() ?? string.Empty,
                heading,
                sourceFile,
                null,
                technicalDetails
            ));
        }

        return stories
            .OrderBy(story => story.Number)
            .ToArray();
    }

    private static int ParseIssueNumber(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return 0;
        }

        var digits = new string(id.Where(character => char.IsDigit(character)).ToArray());
        return int.TryParse(digits, out var parsed) ? parsed : 0;
    }

    private static IReadOnlyList<string> ReadTechnicalDetails(JsonElement story)
    {
        if (!story.TryGetProperty("devNotes", out var devNotes) ||
            devNotes.ValueKind != JsonValueKind.Object ||
            !devNotes.TryGetProperty("technicalDetails", out var technicalDetails) ||
            technicalDetails.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return technicalDetails
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .OfType<string>()
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
