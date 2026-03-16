using System.Text.Json;
using Dragon.Backend.Contracts;
using Dragon.Backend.Orchestrator;

var command = args.FirstOrDefault() ?? "help";
var options = ParseOptions(args.Skip(1).ToArray());

return command switch
{
    "plan" => RunPlan(options),
    "plan-from-backlog" => RunPlanFromBacklog(options),
    _ => ShowHelp()
};

static int RunPlan(IReadOnlyDictionary<string, string> options)
{
    if (!options.TryGetValue("title", out var title) || string.IsNullOrWhiteSpace(title))
    {
        Console.Error.WriteLine("Missing required option: --title");
        return 1;
    }

    var issue = new GithubIssue(
        GetInt(options, "number", 0),
        title,
        "OPEN",
        ["story"],
        GetString(options, "body", string.Empty),
        GetNullable(options, "heading"),
        GetNullable(options, "source-file")
    );

    return PrintPlan(issue);
}

static int RunPlanFromBacklog(IReadOnlyDictionary<string, string> options)
{
    if (!options.TryGetValue("title", out var title) || string.IsNullOrWhiteSpace(title))
    {
        Console.Error.WriteLine("Missing required option: --title");
        return 1;
    }

    var root = GetString(options, "root", Directory.GetCurrentDirectory());
    var index = BacklogIndexLoader.Load(root);

    if (!index.TryGetValue(title, out var metadata))
    {
        Console.Error.WriteLine($"Backlog title not found: {title}");
        return 1;
    }

    var issue = new GithubIssue(
        GetInt(options, "number", 0),
        title,
        "OPEN",
        ["story"],
        GetString(options, "body", string.Empty),
        metadata.Heading,
        metadata.SourceFile
    );

    return PrintPlan(issue);
}

static int PrintPlan(GithubIssue issue)
{
    var job = SelfBuildJobFactory.Create(issue, "developer", "IdeaEngine", "DragonIdeaEngine");
    var json = JsonSerializer.Serialize(job, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    Console.WriteLine(json);
    return 0;
}

static int ShowHelp()
{
    Console.WriteLine(
        """
        Dragon.Backend.Cli

        Commands:
          plan --title <story-title> [--number 22] [--heading <heading>] [--source-file <path>] [--body <text>]
          plan-from-backlog --title <story-title> [--number 22] [--body <text>] [--root <repo-root>]
        """
    );

    return 0;
}

static Dictionary<string, string> ParseOptions(string[] values)
{
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < values.Length; index += 1)
    {
        var current = values[index];
        if (!current.StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = current[2..];
        var value = index + 1 < values.Length && !values[index + 1].StartsWith("--", StringComparison.Ordinal)
            ? values[++index]
            : "true";

        options[key] = value;
    }

    return options;
}

static string GetString(IReadOnlyDictionary<string, string> options, string key, string fallback) =>
    options.TryGetValue(key, out var value) ? value : fallback;

static string? GetNullable(IReadOnlyDictionary<string, string> options, string key) =>
    options.TryGetValue(key, out var value) ? value : null;

static int GetInt(IReadOnlyDictionary<string, string> options, string key, int fallback) =>
    options.TryGetValue(key, out var rawValue) && int.TryParse(rawValue, out var parsedValue)
        ? parsedValue
        : fallback;
