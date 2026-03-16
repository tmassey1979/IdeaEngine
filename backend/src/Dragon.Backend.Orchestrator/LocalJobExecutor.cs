using Dragon.Backend.Contracts;
using System.Diagnostics;
using System.Text.Json;

namespace Dragon.Backend.Orchestrator;

public delegate CommandResult LocalCommandRunner(string fileName, string arguments, string workingDirectory);

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class LocalJobExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LocalCommandRunner commandRunner;

    public LocalJobExecutor(LocalCommandRunner? commandRunner = null)
    {
        this.commandRunner = commandRunner ?? RunCommand;
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
                _ => ExecutionOutcome.FromSummary($"No local executor is registered for {job.Agent}; marked complete for bootstrap flow.")
            };

            return new JobExecutionResult(jobId, job.Agent, "success", outcome.Summary, DateTimeOffset.UtcNow, outcome.ChangedPaths);
        }
        catch (Exception exception)
        {
            return new JobExecutionResult(jobId, job.Agent, "failed", exception.Message, DateTimeOffset.UtcNow);
        }
    }

    private static ExecutionOutcome ExecuteDeveloper(string rootDirectory, SelfBuildJob job)
    {
        var operations = job.Payload.Operations ?? [];
        var touchedPaths = new List<string>();

        foreach (var operation in operations)
        {
            var fullPath = Path.Combine(rootDirectory, operation.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            switch (operation.Type)
            {
                case "write_file":
                    File.WriteAllText(fullPath, operation.Content ?? string.Empty);
                    break;
                case "append_text":
                    File.AppendAllText(fullPath, operation.Content ?? string.Empty);
                    break;
                case "replace_text":
                    var source = File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
                    if (string.IsNullOrEmpty(operation.SearchText) || !source.Contains(operation.SearchText, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Replace target was not found in {operation.Path}.");
                    }

                    File.WriteAllText(fullPath, source.Replace(operation.SearchText, operation.ReplaceWith ?? string.Empty, StringComparison.Ordinal));
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported developer operation: {operation.Type}");
            }

            touchedPaths.Add(operation.Path);
        }

        return new ExecutionOutcome(
            touchedPaths.Count > 0
                ? $"Applied {touchedPaths.Count} developer operation(s): {string.Join(", ", touchedPaths)}"
                : "Developer job contained no operations.",
            touchedPaths
        );
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

    private sealed record ExecutionOutcome(string Summary, IReadOnlyList<string> ChangedPaths)
    {
        public static ExecutionOutcome FromSummary(string summary) => new(summary, []);
    }
}
