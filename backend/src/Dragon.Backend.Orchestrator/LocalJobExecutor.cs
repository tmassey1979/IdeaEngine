using Dragon.Backend.Contracts;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Dragon.Backend.Orchestrator;

public delegate CommandResult LocalCommandRunner(string fileName, string arguments, string workingDirectory);

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
public sealed record ModelExecutionRetryOptions(int MaxAttempts = 3, int BaseDelayMilliseconds = 1000);

public sealed class LocalJobExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LocalCommandRunner commandRunner;
    private readonly IAgentModelProvider? modelProvider;
    private readonly ModelExecutionRetryOptions modelRetryOptions;
    private readonly Action<TimeSpan> sleepAction;

    public LocalJobExecutor(
        LocalCommandRunner? commandRunner = null,
        IAgentModelProvider? modelProvider = null,
        ModelExecutionRetryOptions? modelRetryOptions = null,
        Action<TimeSpan>? sleepAction = null)
    {
        this.commandRunner = commandRunner ?? RunCommand;
        this.modelProvider = modelProvider;
        this.modelRetryOptions = modelRetryOptions ?? new ModelExecutionRetryOptions();
        this.sleepAction = sleepAction ?? Thread.Sleep;
    }

    public static LocalJobExecutor CreateDefault(Func<string, string?> environmentReader, LocalCommandRunner? commandRunner = null)
    {
        IAgentModelProvider? provider = null;

        try
        {
            provider = new OpenAiResponsesProvider(OpenAiResponsesOptions.FromEnvironment(environmentReader));
        }
        catch (InvalidOperationException)
        {
            provider = null;
        }

        return new LocalJobExecutor(commandRunner, provider);
    }

    public JobExecutionResult Execute(string rootDirectory, SelfBuildJob job)
    {
        var jobId = $"{job.Agent}-{job.Issue}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        try
        {
            var outcome = job.Agent switch
            {
                "developer" => ExecuteDeveloper(rootDirectory, job),
                "review" => ExecutionOutcome.FromSummary(ExecuteReview(rootDirectory, job)),
                "test" => ExecutionOutcome.FromSummary(ExecuteTest(rootDirectory, job)),
                _ when IsModelBackedAgent(job.Agent) => ExecuteModelBacked(rootDirectory, job),
                _ => ExecutionOutcome.FromSummary($"No local executor is registered for {job.Agent}; marked complete for bootstrap flow.")
            };

            return new JobExecutionResult(jobId, job.Agent, "success", outcome.Summary, DateTimeOffset.UtcNow, outcome.ChangedPaths, outcome.RequestedFollowUps);
        }
        catch (Exception exception)
        {
            return new JobExecutionResult(
                jobId,
                job.Agent,
                "failed",
                FormatFailureSummary(exception),
                DateTimeOffset.UtcNow,
                RetryNotBefore: ResolveRetryNotBefore(exception));
        }
    }

    private ExecutionOutcome ExecuteModelBacked(string rootDirectory, SelfBuildJob job)
    {
        if (modelProvider is null)
        {
            if (job.Payload.Operations?.Count > 0)
            {
                var changedPaths = ApplyOperations(rootDirectory, job.Payload.Operations);
                return new ExecutionOutcome(
                    changedPaths.Count > 0
                        ? $"Applied {changedPaths.Count} planned {job.Agent} operation(s): {string.Join(", ", changedPaths)}"
                        : $"{job.Agent} planned operations were already up to date.",
                    changedPaths,
                    []);
            }

            return ExecutionOutcome.FromSummary($"No model provider configured for {job.Agent}; marked complete for bootstrap flow.");
        }

        var request = AgentPromptFactory.Build(job, modelProvider.Describe().DefaultModel);
        var response = ExecuteModelRequestWithRetry(request);
        var structuredResult = AgentStructuredResultParser.Parse(response.OutputText);
        var summary = !string.IsNullOrWhiteSpace(structuredResult?.Summary)
            ? structuredResult.Summary
            : string.IsNullOrWhiteSpace(response.OutputText)
            ? $"{job.Agent} completed through {response.Provider}."
            : response.OutputText.Trim();

        if (structuredResult?.Operations?.Count > 0)
        {
            var changedPaths = ApplyOperations(rootDirectory, structuredResult.Operations);
            return new ExecutionOutcome(summary, changedPaths, structuredResult.FollowUps ?? []);
        }

        return new ExecutionOutcome(summary, [], structuredResult?.FollowUps ?? []);
    }

    private AgentModelResponse ExecuteModelRequestWithRetry(AgentModelRequest request)
    {
        Exception? lastException = null;
        var maxAttempts = Math.Max(1, modelRetryOptions.MaxAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return modelProvider!.GenerateAsync(request).GetAwaiter().GetResult();
            }
            catch (Exception exception) when (attempt < maxAttempts && IsTransientModelFailure(exception))
            {
                lastException = exception;
                SleepBeforeModelRetry(exception, attempt);
            }
        }

        throw lastException ?? new InvalidOperationException("Model execution failed without an exception.");
    }

    private static ExecutionOutcome ExecuteDeveloper(string rootDirectory, SelfBuildJob job)
    {
        var touchedPaths = ApplyOperations(rootDirectory, job.Payload.Operations ?? []);

        return new ExecutionOutcome(
            touchedPaths.Count > 0
                ? $"Applied {touchedPaths.Count} developer operation(s): {string.Join(", ", touchedPaths)}"
                : "Developer job contained no operations.",
            touchedPaths,
            []
        );
    }

    private static IReadOnlyList<string> ApplyOperations(string rootDirectory, IReadOnlyList<DeveloperOperation> operations)
    {
        var touchedPaths = new List<string>();

        foreach (var operation in operations)
        {
            var fullPath = Path.Combine(rootDirectory, operation.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            switch (operation.Type)
            {
                case "write_file":
                    File.WriteAllText(fullPath, operation.Content ?? string.Empty);
                    touchedPaths.Add(operation.Path);
                    break;
                case "append_text":
                    var appendContent = operation.Content ?? string.Empty;
                    var existingContent = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
                    if (string.IsNullOrEmpty(appendContent) || !existingContent.Contains(appendContent, StringComparison.Ordinal))
                    {
                        File.AppendAllText(fullPath, appendContent);
                        touchedPaths.Add(operation.Path);
                    }

                    break;
                case "replace_text":
                    var source = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
                    if (string.IsNullOrEmpty(operation.SearchText) || !source.Contains(operation.SearchText, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Replace target was not found in {operation.Path}.");
                    }

                    File.WriteAllText(fullPath, source.Replace(operation.SearchText, operation.ReplaceWith ?? string.Empty, StringComparison.Ordinal));
                    touchedPaths.Add(operation.Path);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported developer operation: {operation.Type}");
            }
        }

        return touchedPaths;
    }

    private string ExecuteReview(string rootDirectory, SelfBuildJob job)
    {
        var workflowState = LoadWorkflowState(rootDirectory, job.Issue);
        var implementationStage = workflowState is null ? null : FindSuccessfulImplementationStage(workflowState);
        if (implementationStage is null)
        {
            throw new InvalidOperationException("Review failed because no implementation stage has completed successfully.");
        }

        var changedPaths = ReadChangedPaths(job);
        if (changedPaths.Count == 0)
        {
            return $"Review completed for implementation stage {implementationStage} with no changed paths supplied.";
        }

        foreach (var relativePath in changedPaths)
        {
            var fullPath = Path.Combine(rootDirectory, relativePath);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException($"Review failed because {relativePath} does not exist.");
            }

            var content = File.ReadAllText(fullPath);
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException($"Review failed because {relativePath} is empty.");
            }
        }

        return $"Review completed for {changedPaths.Count} changed file(s): {string.Join(", ", changedPaths)}";
    }

    private string ExecuteTest(string rootDirectory, SelfBuildJob job)
    {
        var workflowState = LoadWorkflowState(rootDirectory, job.Issue);
        if (workflowState is null ||
            !workflowState.Stages.TryGetValue("review", out var reviewStage) ||
            !string.Equals(reviewStage.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Test failed because the review stage has not completed successfully.");
        }

        var packageJson = Path.Combine(rootDirectory, "package.json");
        var solution = Path.Combine(rootDirectory, "backend", "Dragon.Backend.slnx");

        if (File.Exists(solution))
        {
            var result = commandRunner(ResolveCommand("dotnet"), $"test \"{solution}\"", rootDirectory);
            EnsureSuccess(result, "dotnet test");
            return "Executed dotnet test against backend/Dragon.Backend.slnx.";
        }

        if (File.Exists(packageJson))
        {
            var script = SelectPackageScript(packageJson);
            if (script is null)
            {
                throw new InvalidOperationException("Test stage found package.json but no runnable test script.");
            }

            var command = script == "test" ? "test" : $"run {script}";
            var result = commandRunner(ResolveCommand("npm"), command, rootDirectory);
            EnsureSuccess(result, $"npm {command}");
            return $"Executed npm {command}.";
        }

        throw new InvalidOperationException("Test stage found no recognizable project entrypoint.");
    }

    private static IssueWorkflowState? LoadWorkflowState(string rootDirectory, int issueNumber)
    {
        var statePath = Path.Combine(rootDirectory, ".dragon", "state", "issues.json");
        if (!File.Exists(statePath))
        {
            return null;
        }

        var snapshots = JsonSerializer.Deserialize<List<IssueWorkflowState>>(File.ReadAllText(statePath), JsonOptions);
        return snapshots?.FirstOrDefault(item => item.IssueNumber == issueNumber);
    }

    private static IReadOnlyList<string> ReadChangedPaths(SelfBuildJob job)
    {
        if (!job.Metadata.TryGetValue("changedPaths", out var changedPathsRaw) || string.IsNullOrWhiteSpace(changedPathsRaw))
        {
            return [];
        }

        return changedPathsRaw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? FindSuccessfulImplementationStage(IssueWorkflowState workflowState)
    {
        var implementationStage = workflowState.Stages
            .Where(stage => !string.Equals(stage.Key, "review", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(stage.Key, "test", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(stage.Value.Status, "success", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(stage => string.Equals(stage.Key, "developer", StringComparison.OrdinalIgnoreCase))
            .ThenBy(stage => stage.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return implementationStage.Equals(default(KeyValuePair<string, WorkflowStageState>))
            ? null
            : implementationStage.Key;
    }

    private static string? SelectPackageScript(string packageJsonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
        if (!document.RootElement.TryGetProperty("scripts", out var scripts))
        {
            return null;
        }

        if (scripts.TryGetProperty("test:backend", out _))
        {
            return "test:backend";
        }

        if (scripts.TryGetProperty("test", out _))
        {
            return "test";
        }

        return null;
    }

    private static void EnsureSuccess(CommandResult result, string commandDescription)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{commandDescription} failed: {result.StandardError}".Trim());
        }
    }

    private static string ResolveCommand(string baseName)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"{baseName}.cmd";
        }

        return baseName;
    }

    private static CommandResult RunCommand(string fileName, string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new CommandResult(process.ExitCode, stdout, stderr);
    }

    private void SleepBeforeModelRetry(Exception exception, int attempt)
    {
        var retryDelay = ResolveRetryDelay(exception, attempt);
        if (retryDelay <= TimeSpan.Zero)
        {
            return;
        }

        sleepAction(retryDelay);
    }

    private static bool IsTransientModelFailure(Exception exception)
    {
        return exception switch
        {
            AgentModelProviderException providerException => providerException.IsTransient,
            HttpRequestException httpRequestException => IsTransientStatusCode(httpRequestException.StatusCode),
            TaskCanceledException => true,
            TimeoutException => true,
            _ => false
        };
    }

    private static bool IsTransientStatusCode(HttpStatusCode? statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.RequestTimeout => true,
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            var code when code is >= HttpStatusCode.InternalServerError => true,
            _ => false
        };
    }

    private TimeSpan ResolveRetryDelay(Exception exception, int attempt)
    {
        if (exception is AgentModelProviderException providerException &&
            providerException.RetryAfter is { } retryAfter &&
            retryAfter > TimeSpan.Zero)
        {
            return retryAfter;
        }

        var baseDelayMilliseconds = Math.Max(0, modelRetryOptions.BaseDelayMilliseconds);
        if (baseDelayMilliseconds == 0)
        {
            return TimeSpan.Zero;
        }

        var multiplier = Math.Max(1, attempt);
        return TimeSpan.FromMilliseconds(baseDelayMilliseconds * multiplier);
    }

    private static string FormatFailureSummary(Exception exception)
    {
        if (exception is AgentModelProviderException providerException)
        {
            var status = providerException.StatusCode is null
                ? null
                : $"HTTP {(int)providerException.StatusCode.Value}";
            var retryAfter = providerException.RetryAfter is null
                ? null
                : $"retry after {FormatRetryAfter(providerException.RetryAfter.Value)}";
            var qualifiers = new[] { status, retryAfter }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            var suffix = qualifiers.Length == 0 ? string.Empty : $" ({string.Join(", ", qualifiers)})";
            var prefix = providerException.IsTransient
                ? "Transient model provider failure"
                : "Model provider failure";

            return $"{prefix} from {providerException.Provider}{suffix}: {providerException.Message}";
        }

        return exception.Message;
    }

    private static DateTimeOffset? ResolveRetryNotBefore(Exception exception)
    {
        return exception is AgentModelProviderException providerException &&
            providerException.RetryAfter is { } retryAfter &&
            retryAfter > TimeSpan.Zero
                ? DateTimeOffset.UtcNow.Add(retryAfter)
                : null;
    }

    private static string FormatRetryAfter(TimeSpan retryAfter)
    {
        if (retryAfter.TotalMinutes >= 1 && retryAfter.Seconds == 0)
        {
            return $"{Math.Floor(retryAfter.TotalMinutes)}m";
        }

        if (retryAfter.TotalSeconds >= 1)
        {
            return $"{Math.Ceiling(retryAfter.TotalSeconds)}s";
        }

        return "0s";
    }

    private sealed record ExecutionOutcome(
        string Summary,
        IReadOnlyList<string> ChangedPaths,
        IReadOnlyList<RequestedFollowUp> RequestedFollowUps)
    {
        public static ExecutionOutcome FromSummary(string summary) => new(summary, [], []);
    }

    private static bool IsModelBackedAgent(string agent) => agent switch
    {
        "architect" => true,
        "documentation" => true,
        "feedback" => true,
        "idea" => true,
        "repository-manager" => true,
        "refactor" => true,
        _ => false
    };
}
