using System.Net;
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
    "serve-status" => RunServeStatus(options),
    "queue" => RunQueue(options),
    "cycle-once" => RunCycleOnce(options),
    "run-until-idle" => RunUntilIdle(options),
    "run-polling" => RunPolling(options),
    "run-watch" => RunWatch(options),
    "github-issues" => RunGithubIssues(options),
    "github-cycle-once" => RunGithubCycleOnce(options),
    "github-run-until-idle" => RunGithubRunUntilIdle(options),
    "github-run-polling" => RunGithubRunPolling(options),
    "github-run-watch" => RunGithubRunWatch(options),
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
        ? loop.ReadStatus("status", "status", "snapshot")
        : loop.WriteStatus(Path.GetFullPath(outputPath, root), "status", "status", "snapshot");

    PrintJson(snapshot);
    return 0;
}

static int RunServeStatus(IReadOnlyDictionary<string, string> options)
{
    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var prefix = GetString(options, "prefix", "http://127.0.0.1:5078/");
    var snapshotPath = GetNullable(options, "snapshot-file");
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler? handler = null;
    handler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    Console.CancelKeyPress += handler;

    try
    {
        var loop = new SelfBuildLoop(root);
        var server = new StatusHttpServer(
            loop,
            string.IsNullOrWhiteSpace(snapshotPath) ? null : Path.GetFullPath(snapshotPath, root));
        Console.WriteLine($"Serving status on {prefix}");
        server.ServeUntilCancelledAsync(prefix, cancellation.Token).GetAwaiter().GetResult();
        return 0;
    }
    catch (HttpListenerException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
    finally
    {
        Console.CancelKeyPress -= handler;
    }
}

static int RunCycleOnce(IReadOnlyDictionary<string, string> options)
{
    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var stories = BacklogStoryCatalog.LoadStories(root);
    var loop = new SelfBuildLoop(root);
    var result = loop.CycleOnce(stories);
    ExportStatusIfRequested(loop, root, options, "cycle-once");
    PrintJson(result);
    return 0;
}

static int RunUntilIdle(IReadOnlyDictionary<string, string> options)
{
    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var stories = BacklogStoryCatalog.LoadStories(root);
    var loop = new SelfBuildLoop(root);
    var result = loop.RunUntilIdle(
        stories,
        maxCycles: GetInt(options, "max-cycles", 100)
    );
    ExportStatusIfRequested(
        loop,
        root,
        options,
        "run-until-idle",
        SelfBuildLoop.BuildLatestPassSummary(1, result),
        "complete",
        workerCompletionReason: result.ReachedIdle ? "idle_run_completed" : "max_cycles_reached",
        currentPassNumber: 1,
        maxPasses: 1);
    PrintJson(result);
    return 0;
}

static int RunPolling(IReadOnlyDictionary<string, string> options)
{
    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var stories = BacklogStoryCatalog.LoadStories(root);
    var loop = new SelfBuildLoop(root);
    var maxPasses = GetInt(options, "max-passes", 10);
    var statusExporter = CreateStatusExporter(loop, root, options, "run-polling", "complete", maxPasses: maxPasses);
    var result = loop.RunPolling(
        stories,
        maxPasses: maxPasses,
        idlePassesBeforeStop: GetInt(options, "idle-passes", 2),
        maxCyclesPerPass: GetInt(options, "max-cycles", 100),
        passCompleted: statusExporter
    );
    PrintJson(result);
    return 0;
}

static int RunWatch(IReadOnlyDictionary<string, string> options)
{
    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var stories = BacklogStoryCatalog.LoadStories(root);
    var loop = new SelfBuildLoop(root);
    var pollInterval = TimeSpan.FromSeconds(GetInt(options, "poll-seconds", 30));
    var maxPasses = GetInt(options, "max-passes", 10);
    var idlePassesBeforeStop = GetInt(options, "idle-passes", 2);
    var consecutiveIdlePasses = 0;
    var statusExporter = CreateStatusExporter(
        loop,
        root,
        options,
        "run-watch",
        "complete",
        initialPollIntervalSeconds: (int)pollInterval.TotalSeconds,
        currentIdleStreak: () => consecutiveIdlePasses,
        idleTarget: Math.Max(1, idlePassesBeforeStop),
        currentIdlePassesRemaining: () => Math.Max(0, Math.Max(1, idlePassesBeforeStop) - consecutiveIdlePasses),
        maxPasses: maxPasses,
        currentPassBudgetRemaining: passNumber => Math.Max(0, maxPasses - passNumber),
        workerStatusResolver: (passNumber, result) =>
        {
            var requiredIdlePasses = Math.Max(1, idlePassesBeforeStop);
            consecutiveIdlePasses = result.ReachedIdle ? consecutiveIdlePasses + 1 : 0;
            var willContinue = passNumber < maxPasses && consecutiveIdlePasses < requiredIdlePasses;
            return (
                willContinue ? "waiting" : "complete",
                willContinue ? DateTimeOffset.UtcNow.Add(pollInterval) : null,
                willContinue ? null : (consecutiveIdlePasses >= requiredIdlePasses ? "idle_target_reached" : "max_passes_reached")
            );
        });
    var result = loop.RunWatching(
        stories,
        pollInterval,
        maxPasses: maxPasses,
        idlePassesBeforeStop: idlePassesBeforeStop,
        maxCyclesPerPass: GetInt(options, "max-cycles", 100),
        delayAction: Thread.Sleep,
        passCompleted: statusExporter);
    PrintJson(result);
    return 0;
}

static int RunGithubIssues(IReadOnlyDictionary<string, string> options)
{
    if (!TryGetRepoOptions(options, out var owner, out var repo))
    {
        return 1;
    }

    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
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

    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var loop = new SelfBuildLoop(root);
    var result = loop.CycleOnceFromGithub(owner!, repo!, syncValidatedWorkflows: GetBoolean(options, "sync-github"));
    ExportStatusIfRequested(loop, root, options, "github-cycle-once");
    PrintJson(result);
    return 0;
}

static int RunGithubRunUntilIdle(IReadOnlyDictionary<string, string> options)
{
    if (!TryGetRepoOptions(options, out var owner, out var repo))
    {
        return 1;
    }

    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var loop = new SelfBuildLoop(root);
    var result = loop.RunUntilIdleFromGithub(
        owner!,
        repo!,
        syncValidatedWorkflows: GetBoolean(options, "sync-github"),
        maxCycles: GetInt(options, "max-cycles", 100)
    );
    ExportStatusIfRequested(
        loop,
        root,
        options,
        "github-run-until-idle",
        SelfBuildLoop.BuildLatestPassSummary(1, result),
        "complete",
        workerCompletionReason: result.ReachedIdle ? "idle_run_completed" : "max_cycles_reached",
        currentPassNumber: 1,
        maxPasses: 1);
    PrintJson(result);
    return 0;
}

static int RunGithubRunPolling(IReadOnlyDictionary<string, string> options)
{
    if (!TryGetRepoOptions(options, out var owner, out var repo))
    {
        return 1;
    }

    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var loop = new SelfBuildLoop(root);
    var maxPasses = GetInt(options, "max-passes", 10);
    var statusExporter = CreateStatusExporter(loop, root, options, "github-run-polling", "complete", maxPasses: maxPasses);
    var result = loop.RunPollingFromGithub(
        owner!,
        repo!,
        syncValidatedWorkflows: GetBoolean(options, "sync-github"),
        maxPasses: maxPasses,
        idlePassesBeforeStop: GetInt(options, "idle-passes", 2),
        maxCyclesPerPass: GetInt(options, "max-cycles", 100),
        passCompleted: statusExporter
    );
    PrintJson(result);
    return 0;
}

static int RunGithubRunWatch(IReadOnlyDictionary<string, string> options)
{
    if (!TryGetRepoOptions(options, out var owner, out var repo))
    {
        return 1;
    }

    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var loop = new SelfBuildLoop(root);
    var pollInterval = TimeSpan.FromSeconds(GetInt(options, "poll-seconds", 30));
    var maxPasses = GetInt(options, "max-passes", 10);
    var idlePassesBeforeStop = GetInt(options, "idle-passes", 2);
    var consecutiveIdlePasses = 0;
    var statusExporter = CreateStatusExporter(
        loop,
        root,
        options,
        "github-run-watch",
        "complete",
        initialPollIntervalSeconds: (int)pollInterval.TotalSeconds,
        currentIdleStreak: () => consecutiveIdlePasses,
        idleTarget: Math.Max(1, idlePassesBeforeStop),
        currentIdlePassesRemaining: () => Math.Max(0, Math.Max(1, idlePassesBeforeStop) - consecutiveIdlePasses),
        maxPasses: maxPasses,
        currentPassBudgetRemaining: passNumber => Math.Max(0, maxPasses - passNumber),
        workerStatusResolver: (passNumber, result) =>
        {
            var requiredIdlePasses = Math.Max(1, idlePassesBeforeStop);
            consecutiveIdlePasses = result.ReachedIdle ? consecutiveIdlePasses + 1 : 0;
            var willContinue = passNumber < maxPasses && consecutiveIdlePasses < requiredIdlePasses;
            return (
                willContinue ? "waiting" : "complete",
                willContinue ? DateTimeOffset.UtcNow.Add(pollInterval) : null,
                willContinue ? null : (consecutiveIdlePasses >= requiredIdlePasses ? "idle_target_reached" : "max_passes_reached")
            );
        });
    var result = loop.RunWatchingFromGithub(
        owner!,
        repo!,
        pollInterval,
        syncValidatedWorkflows: GetBoolean(options, "sync-github"),
        maxPasses: maxPasses,
        idlePassesBeforeStop: idlePassesBeforeStop,
        maxCyclesPerPass: GetInt(options, "max-cycles", 100),
        delayAction: Thread.Sleep,
        passCompleted: statusExporter);
    PrintJson(result);
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

    var root = Path.GetFullPath(GetString(options, "root", Directory.GetCurrentDirectory()));
    var loop = new SelfBuildLoop(root);
    PrintJson(loop.SyncValidatedWorkflow(owner!, repo!, issueNumber));
    return 0;
}

static void ExportStatusIfRequested(
    SelfBuildLoop loop,
    string root,
    IReadOnlyDictionary<string, string> options,
    string source,
    LatestPassSummary? latestPass = null,
    string workerState = "complete",
    string? workerCompletionReason = null,
    int? currentPassNumber = null,
    int? maxPasses = null)
{
    var exporter = CreateStatusExporter(loop, root, options, source, workerState, initialLatestPass: latestPass, maxPasses: maxPasses, initialPassNumber: currentPassNumber, initialWorkerCompletionReason: workerCompletionReason);
    if (exporter is null)
    {
        return;
    }

    exporter(1, new RunUntilIdleResult([], true, false));
}

static Action<int, RunUntilIdleResult>? CreateStatusExporter(
    SelfBuildLoop loop,
    string root,
    IReadOnlyDictionary<string, string> options,
    string source,
    string defaultWorkerState,
    int? initialPollIntervalSeconds = null,
    Func<int>? currentIdleStreak = null,
    int idleTarget = 0,
    Func<int>? currentIdlePassesRemaining = null,
    int? maxPasses = null,
    Func<int, int>? currentPassBudgetRemaining = null,
    Func<int, RunUntilIdleResult, (string WorkerState, DateTimeOffset? NextPollAt, string? WorkerCompletionReason)>? workerStatusResolver = null,
    LatestPassSummary? initialLatestPass = null,
    int? initialPassNumber = null,
    string? initialWorkerCompletionReason = null)
{
    var outputPath = GetNullable(options, "status-out");
    if (string.IsNullOrWhiteSpace(outputPath))
    {
        return null;
    }

    var resolvedOutputPath = Path.GetFullPath(outputPath, root);
    return (passNumber, result) =>
    {
        var latestPass = initialLatestPass ?? SelfBuildLoop.BuildLatestPassSummary(passNumber, result);
        var workerStatus = workerStatusResolver?.Invoke(passNumber, result) ?? (defaultWorkerState, (DateTimeOffset?)null, initialWorkerCompletionReason);
        var snapshot = loop.ReadStatus(
            source,
            source,
            workerStatus.WorkerState,
            workerStatus.WorkerCompletionReason,
            workerStatus.NextPollAt,
            initialPollIntervalSeconds,
            currentIdleStreak?.Invoke() ?? 0,
            idleTarget,
            currentIdlePassesRemaining?.Invoke(),
            currentPassBudgetRemaining?.Invoke(passNumber),
            latestPass,
            initialPassNumber ?? passNumber,
            maxPasses) with
        {
            Source = source
        };
        WriteStatusSnapshot(resolvedOutputPath, snapshot);
    };
}

static void WriteStatusSnapshot(string outputPath, StatusSnapshot snapshot)
{
    StatusSnapshot? previousSnapshot = null;
    if (File.Exists(outputPath))
    {
        previousSnapshot = JsonSerializer.Deserialize<StatusSnapshot>(
            File.ReadAllText(outputPath),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
    }

    snapshot = StatusSnapshotTrend.Apply(snapshot, previousSnapshot);

    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    File.WriteAllText(outputPath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }));
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
          serve-status [--root <repo-root>] [--prefix http://127.0.0.1:5078/] [--snapshot-file <path>]
          queue [--root <repo-root>]
          cycle-once [--root <repo-root>] [--status-out <path>]
          run-until-idle [--max-cycles 100] [--root <repo-root>] [--status-out <path>]
          run-polling [--max-passes 10] [--idle-passes 2] [--max-cycles 100] [--root <repo-root>] [--status-out <path>]
          run-watch [--poll-seconds 30] [--max-passes 10] [--idle-passes 2] [--max-cycles 100] [--root <repo-root>] [--status-out <path>]
          github-issues --owner <owner> --repo <repo> [--root <repo-root>]
          github-cycle-once --owner <owner> --repo <repo> [--sync-github] [--root <repo-root>] [--status-out <path>]
          github-run-until-idle --owner <owner> --repo <repo> [--sync-github] [--max-cycles 100] [--root <repo-root>] [--status-out <path>]
          github-run-polling --owner <owner> --repo <repo> [--sync-github] [--max-passes 10] [--idle-passes 2] [--max-cycles 100] [--root <repo-root>] [--status-out <path>]
          github-run-watch --owner <owner> --repo <repo> [--sync-github] [--poll-seconds 30] [--max-passes 10] [--idle-passes 2] [--max-cycles 100] [--root <repo-root>] [--status-out <path>]
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
