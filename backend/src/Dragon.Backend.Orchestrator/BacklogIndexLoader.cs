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

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(heading) || string.IsNullOrWhiteSpace(sourceFile))
            {
                continue;
            }

            index[title] = new BacklogStoryMetadata(title, heading, sourceFile);
        }

        return index;
    }
}
