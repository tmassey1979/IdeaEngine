using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static class BacklogIndexLoader
{
    public static IReadOnlyDictionary<string, BacklogStoryMetadata> Load(string rootDirectory)
    {
        var backlogPath = Path.Combine(rootDirectory, "planning", "backlog.json");

        if (!File.Exists(backlogPath))
        {
            throw new FileNotFoundException("Could not find planning/backlog.json.", backlogPath);
        }

        using var stream = File.OpenRead(backlogPath);
        using var document = JsonDocument.Parse(stream);

        var stories = document.RootElement.GetProperty("stories");
        var index = new Dictionary<string, BacklogStoryMetadata>(StringComparer.Ordinal);

        foreach (var story in stories.EnumerateArray())
        {
            var title = story.GetProperty("title").GetString();
            var heading = story.GetProperty("heading").GetString();
            var sourceFile = story.GetProperty("sourceFile").GetString();
            var technicalDetails = ReadTechnicalDetails(story);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(heading) || string.IsNullOrWhiteSpace(sourceFile))
            {
                continue;
            }

            index[title] = new BacklogStoryMetadata(title, heading, sourceFile, technicalDetails);
        }

        return index;
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
