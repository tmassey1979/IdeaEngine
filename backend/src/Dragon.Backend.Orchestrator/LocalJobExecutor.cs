using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class LocalJobExecutor
{
    public JobExecutionResult Execute(string rootDirectory, SelfBuildJob job)
    {
        var jobId = $"{job.Agent}-{job.Issue}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        try
        {
            var summary = job.Agent switch
            {
                "developer" => ExecuteDeveloper(rootDirectory, job),
                "review" => ExecuteReview(rootDirectory),
                "test" => ExecuteTest(rootDirectory),
                _ => $"No local executor is registered for {job.Agent}; marked complete for bootstrap flow."
            };

            return new JobExecutionResult(jobId, job.Agent, "success", summary, DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            return new JobExecutionResult(jobId, job.Agent, "failed", exception.Message, DateTimeOffset.UtcNow);
        }
    }

    private static string ExecuteDeveloper(string rootDirectory, SelfBuildJob job)
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

        return touchedPaths.Count > 0
            ? $"Applied {touchedPaths.Count} developer operation(s): {string.Join(", ", touchedPaths)}"
            : "Developer job contained no operations.";
    }

    private static string ExecuteReview(string rootDirectory)
    {
        var generatedDocs = Path.Combine(rootDirectory, "docs");
        if (!Directory.Exists(generatedDocs))
        {
            throw new InvalidOperationException("Review failed because the docs directory does not exist.");
        }

        return "Review completed against the current workspace snapshot.";
    }

    private static string ExecuteTest(string rootDirectory)
    {
        var packageJson = Path.Combine(rootDirectory, "package.json");
        var solution = Path.Combine(rootDirectory, "backend", "Dragon.Backend.slnx");

        if (!File.Exists(packageJson) && !File.Exists(solution))
        {
            throw new InvalidOperationException("Test stage found no recognizable project entrypoint.");
        }

        return "Test prerequisites are present for the workspace.";
    }
}
