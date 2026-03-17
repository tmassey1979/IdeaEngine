using System.Text.Json;
using Dragon.Backend.Contracts;
using Dragon.Backend.Orchestrator;

var command = args.FirstOrDefault() ?? "help";
var options = ParseOptions(args.Skip(1).ToArray());

return command switch
{
    "provider-describe" => RunProviderDescribe(options),
    "plan" => RunPlan(options),
    "plan-from-backlog" => RunPlanFromBacklog(options),
    "status" => RunStatus(options),
    "queue" => RunQueue(options),
    "cycle-once" => RunCycleOnce(options),
    "run-until-idle" => RunUntilIdle(options),
    "github-issues" => RunGithubIssues(options),
    "github-cycle-once" => RunGithubCycleOnce(options),
    "github-run-until-idle" => RunGithubRunUntilIdle(options),
    "sync-workflow" => RunSyncWorkflow(options),
    _ => ShowHelp()
};

static int RunProviderDescribe(IReadOnlyDictionary<string, string> options)
{
    var provider = GetString(options, "provider", "openai-responses");
    if (!string.Equals(provider, "openai-responses", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine($"Unsupported provider: {provider}");
        return 1;
    }

    try
    {
        var configuredProvider = new OpenAiResponsesProvider(OpenAiResponsesOptions.FromEnvironment(Environment.GetEnvironmentVariable));
        PrintJson(configuredProvider.Describe());
        return 0;
    }
    catch (InvalidOperationException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

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
    PrintJson(job);
    return 0;
}

static int RunQueue(IReadOnlyDictionary<string, string> options)
{
    var root = GetString(options, "root", Directory.GetCurrentDirectory());
    var loop = new SelfBuildLoop(root);
    PrintJson(loop.ReadQueue());
    return 0;
}

static int RunStatus(IReadOnlyDictionary<string, string> options)
{
    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var loop = new SelfBuildLoop(root);
    var outputPath = GetNullable(options, "out");
    var snapshot = string.IsNullOrWhiteSpace(outputPath)
        ? loop.ReadStatus()
        : loop.WriteStatus(Path.GetFullPath(outputPath, root));

    PrintJson(snapshot);
    return 0;
}

static int RunCycleOnce(IReadOnlyDictionary<string, string> options)
{
    var root = GetString(options, "root", Directory.GetCurrentDirectory());
    var stories = BacklogStoryCatalog.LoadStories(root);
    var loop = new SelfBuildLoop(root);
    var result = loop.CycleOnce(stories);
    PrintJson(result);
    return 0;
}

static int RunUntilIdle(IReadOnlyDictionary<string, string> options)
{
    var root = GetString(options, "root", Directory.GetCurrentDirectory());
    var stories = BacklogStoryCatalog.LoadStories(root);
    var loop = new SelfBuildLoop(root);
    var result = loop.RunUntilIdle(
        stories,
        maxCycles: GetInt(options, "max-cycles", 100)
    );
    PrintJson(result);
    return 0;
}

static int RunGithubIssues(IReadOnlyDictionary<string, string> options)
{
    if (!TryGetRepoOptions(options, out var owner, out var repo))
    {
        return 1;
    }

    var root = GetString(options, "root", Directory.GetCurrentDirectory());
    var loop = new SelfBuildLoop(root);
    PrintJson(loop.LoadGithubIssues(owner!, repo!));
    return 0;
}

static int RunGithubCycleOnce(IReadOnlyDictionary<string, string> options)
{
    if (!TryGetRepoOptions(options, out var owner, out var repo))
    {
        return 1;
    }

    var root = GetString(options, "root", Directory.GetCurrentDirectory());
    var loop = new SelfBuildLoop(root);
    PrintJson(loop.CycleOnceFromGithub(owner!, repo!, syncValidatedWorkflows: GetBoolean(options, "sync-github")));
    return 0;
}

static int RunGithubRunUntilIdle(IReadOnlyDictionary<string, string> options)
{
    if (!TryGetRepoOptions(options, out var owner, out var repo))
    {
        return 1;
    }

    var root = GetString(options, "root", Directory.GetCurrentDirectory());
    var loop = new SelfBuildLoop(root);
    PrintJson(loop.RunUntilIdleFromGithub(
        owner!,
        repo!,
        syncValidatedWorkflows: GetBoolean(options, "sync-github"),
        maxCycles: GetInt(options, "max-cycles", 100)
    ));
    return 0;
}

static int RunSyncWorkflow(IReadOnlyDictionary<string, string> options)
{
    if (!TryGetRepoOptions(options, out var owner, out var repo))
    {
        return 1;
    }

    var issueNumber = GetInt(options, "issue", 0);
    if (issueNumber <= 0)
    {
        Console.Error.WriteLine("Missing required option: --issue");
        return 1;
    }

    var root = GetString(options, "root", Directory.GetCurrentDirectory());
    var loop = new SelfBuildLoop(root);
    PrintJson(loop.SyncValidatedWorkflow(owner!, repo!, issueNumber));
    return 0;
}

static void PrintJson<TValue>(TValue value)
{
    var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    Console.WriteLine(json);
}

static int ShowHelp()
{
    Console.WriteLine(
        """
        Dragon.Backend.Cli

        Commands:
          provider-describe [--provider openai-responses]
          plan --title <story-title> [--number 22] [--heading <heading>] [--source-file <path>] [--body <text>]
          plan-from-backlog --title <story-title> [--number 22] [--body <text>] [--root <repo-root>]
          status [--root <repo-root>] [--out <path>]
          queue [--root <repo-root>]
          cycle-once [--root <repo-root>]
          run-until-idle [--max-cycles 100] [--root <repo-root>]
          github-issues --owner <owner> --repo <repo> [--root <repo-root>]
          github-cycle-once --owner <owner> --repo <repo> [--sync-github] [--root <repo-root>]
          github-run-until-idle --owner <owner> --repo <repo> [--sync-github] [--max-cycles 100] [--root <repo-root>]
          sync-workflow --owner <owner> --repo <repo> --issue <number> [--root <repo-root>]
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

static bool GetBoolean(IReadOnlyDictionary<string, string> options, string key) =>
    options.TryGetValue(key, out var rawValue) &&
    (string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(rawValue));

static bool TryGetRepoOptions(IReadOnlyDictionary<string, string> options, out string? owner, out string? repo)
{
    owner = GetNullable(options, "owner");
    repo = GetNullable(options, "repo");

    if (!string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo))
    {
        return true;
    }

    Console.Error.WriteLine("Missing required options: --owner and --repo");
    return false;
}
