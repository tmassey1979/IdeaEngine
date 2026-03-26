using Dragon.Backend.Contracts;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Dragon.Backend.Orchestrator;

public delegate CommandResult LocalCommandRunner(string fileName, string arguments, string workingDirectory);
public delegate CommandResult LocalCommandRunnerWithInput(string fileName, string arguments, string workingDirectory, string? standardInput);

public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);
public sealed record ModelExecutionRetryOptions(int MaxAttempts = 3, int BaseDelayMilliseconds = 1000);

public sealed class LocalJobExecutor
{
    private readonly LocalCommandRunner commandRunner;
    private readonly LocalCommandRunnerWithInput commandRunnerWithInput;
    private readonly IAgentModelProvider? modelProvider;
    private readonly AgentRuntimeConfigurationResolver? configurationResolver;
    private readonly ModelExecutionRetryOptions modelRetryOptions;
    private readonly Action<TimeSpan> sleepAction;
    private readonly bool allowCliFallbackForModelProvider;

    public LocalJobExecutor(
        LocalCommandRunner? commandRunner = null,
        IAgentModelProvider? modelProvider = null,
        AgentRuntimeConfigurationResolver? configurationResolver = null,
        ModelExecutionRetryOptions? modelRetryOptions = null,
        Action<TimeSpan>? sleepAction = null,
        LocalCommandRunnerWithInput? commandRunnerWithInput = null)
    {
        this.commandRunner = commandRunner ?? RunCommand;
        this.commandRunnerWithInput = commandRunnerWithInput ?? RunCommandWithInput;
        this.modelProvider = modelProvider;
        this.configurationResolver = configurationResolver;
        this.modelRetryOptions = modelRetryOptions ?? new ModelExecutionRetryOptions();
        this.sleepAction = sleepAction ?? Thread.Sleep;
        this.allowCliFallbackForModelProvider = commandRunnerWithInput is not null;
    }

    public LocalJobExecutor(
        LocalCommandRunner? commandRunner,
        IAgentModelProvider? modelProvider,
        ModelExecutionRetryOptions? modelRetryOptions,
        Action<TimeSpan>? sleepAction = null,
        LocalCommandRunnerWithInput? commandRunnerWithInput = null)
        : this(commandRunner, modelProvider, null, modelRetryOptions, sleepAction, commandRunnerWithInput)
    {
    }

    public static LocalJobExecutor CreateDefault(
        Func<string, string?> environmentReader,
        LocalCommandRunner? commandRunner = null,
        AgentRuntimeOverrides? runtimeOverrides = null) =>
        CreateDefault(Directory.GetCurrentDirectory(), environmentReader, commandRunner, runtimeOverrides);

    public static LocalJobExecutor CreateDefault(
        string rootDirectory,
        Func<string, string?> environmentReader,
        LocalCommandRunner? commandRunner = null,
        AgentRuntimeOverrides? runtimeOverrides = null)
    {
        var resolver = AgentRuntimeConfigurationResolver.CreateDefault(rootDirectory, environmentReader, runtimeOverrides);
        return new LocalJobExecutor(commandRunner, configurationResolver: resolver);
    }

    public JobExecutionResult Execute(string rootDirectory, SelfBuildJob job)
    {
        var jobId = $"{job.Agent}-{job.Issue}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var stopwatch = Stopwatch.StartNew();

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

            stopwatch.Stop();
            return new JobExecutionResult(
                jobId,
                job.Agent,
                "success",
                outcome.Summary,
                DateTimeOffset.UtcNow,
                outcome.ChangedPaths,
                outcome.RequestedFollowUps,
                DurationMilliseconds: stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new JobExecutionResult(
                jobId,
                job.Agent,
                "failed",
                FormatFailureSummary(exception),
                DateTimeOffset.UtcNow,
                DurationMilliseconds: stopwatch.ElapsedMilliseconds,
                RetryNotBefore: ResolveRetryNotBefore(exception));
        }
    }

    private ExecutionOutcome ExecuteModelBacked(string rootDirectory, SelfBuildJob job)
    {
        var resolvedConfiguration = configurationResolver?.Resolve(job.Agent);
        if (modelProvider is null && resolvedConfiguration is null)
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

            return ExecutionOutcome.FromSummary($"No Codex CLI model configured for {job.Agent}; marked complete for bootstrap flow.");
        }

        var model = resolvedConfiguration?.Model ?? modelProvider?.Describe().DefaultModel ?? "gpt-5";
        var request = AgentPromptFactory.Build(job, model);
        AgentModelResponse response;
        if (modelProvider is not null)
        {
            try
            {
                response = ExecuteModelRequestWithRetry(modelProvider, request);
            }
            catch (AgentModelProviderException exception) when (allowCliFallbackForModelProvider && ShouldUseCliFallback(exception))
            {
                response = ExecuteCodexCli(rootDirectory, request);
            }
        }
        else
        {
            response = ExecuteCodexCli(rootDirectory, request);
        }

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

    private AgentModelResponse ExecuteModelRequestWithRetry(IAgentModelProvider provider, AgentModelRequest request)
    {
        Exception? lastException = null;
        var maxAttempts = Math.Max(1, modelRetryOptions.MaxAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return provider.GenerateAsync(request).GetAwaiter().GetResult();
            }
            catch (Exception exception) when (attempt < maxAttempts && IsTransientModelFailure(exception))
            {
                lastException = exception;
                SleepBeforeModelRetry(exception, attempt);
            }
        }

        throw lastException ?? new InvalidOperationException("Model execution failed without an exception.");
    }

    private AgentModelResponse ExecuteCodexCli(string rootDirectory, AgentModelRequest request)
    {
        var artifactsDirectory = Path.Combine(rootDirectory, ".dragon", "artifacts", "codex-cli");
        Directory.CreateDirectory(artifactsDirectory);

        var outputPath = Path.Combine(
            artifactsDirectory,
            $"{request.Agent}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.txt");
        var arguments = string.Join(
            " ",
            [
                "-a",
                "never",
                "exec",
                "--skip-git-repo-check",
                "--sandbox",
                "read-only",
                "-C",
                QuoteArgument(rootDirectory),
                "-m",
                QuoteArgument(request.Model),
                "-o",
                QuoteArgument(outputPath),
                "-"
            ]);

        var result = commandRunnerWithInput(ResolveCommand("codex"), arguments, rootDirectory, BuildCodexPrompt(request));
        if (result.ExitCode != 0)
        {
            var details = BuildCommandFailureDetails(result);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(details)
                    ? "Codex CLI execution failed."
                    : $"Codex CLI execution failed: {details}");
        }

        var outputText = File.Exists(outputPath)
            ? File.ReadAllText(outputPath).Trim()
            : result.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw new InvalidOperationException("Codex CLI execution completed without a final response.");
        }

        return new AgentModelResponse("codex-cli", request.Model, Path.GetFileNameWithoutExtension(outputPath), outputText, "completed");
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

        var profileAwareResult = TryExecuteProfileAwareReview(rootDirectory, job);
        if (!string.IsNullOrWhiteSpace(profileAwareResult))
        {
            return profileAwareResult;
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

    private static string? TryExecuteProfileAwareReview(string rootDirectory, SelfBuildJob job)
    {
        if (IsBackendStackImplementationProfile(job))
        {
            var profileRoot = ResolveBackendStackProfileRoot(rootDirectory, job);
            if (profileRoot is null)
            {
                return null;
            }

            var composePath = Path.Combine(profileRoot, "docker-compose.yml");
            var envExamplePath = Path.Combine(profileRoot, ".env.example");
            var readmePath = Path.Combine(profileRoot, "README.md");

            ValidateRequiredNonEmptyFile(rootDirectory, composePath, "Backend stack review failed because");
            ValidateRequiredNonEmptyFile(rootDirectory, envExamplePath, "Backend stack review failed because");
            ValidateRequiredNonEmptyFile(rootDirectory, readmePath, "Backend stack review failed because");

            return $"Reviewed coordinated backend stack assets in {Path.GetRelativePath(rootDirectory, profileRoot)}.";
        }

        if (!IsDotnetSliceImplementationProfile(job))
        {
            return null;
        }

        var dotnetProfileRoot = ResolveDotnetProfileRoot(rootDirectory, job);
        if (dotnetProfileRoot is null)
        {
            return null;
        }

        var implementationProfile = job.Metadata.GetValueOrDefault("implementationProfile");
        if (string.Equals(implementationProfile, "dotnet/api", StringComparison.OrdinalIgnoreCase))
        {
            ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "Dragon.Api.csproj"), ".NET API review failed because");
            ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "Program.cs"), ".NET API review failed because");
            ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "appsettings.json"), ".NET API review failed because");
            ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "tests", "Dragon.Api.Tests.csproj"), ".NET API review failed because");
            return $"Reviewed .NET API slice assets in {Path.GetRelativePath(rootDirectory, dotnetProfileRoot)}.";
        }

        ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "Dragon.Worker.csproj"), ".NET worker review failed because");
        ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "Program.cs"), ".NET worker review failed because");
        ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "WorkerOptions.cs"), ".NET worker review failed because");
        ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "tests", "Dragon.Worker.Tests.csproj"), ".NET worker review failed because");
        return $"Reviewed .NET worker slice assets in {Path.GetRelativePath(rootDirectory, dotnetProfileRoot)}.";
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

        var profileAwareResult = TryExecuteProfileAwareTest(rootDirectory, job);
        if (!string.IsNullOrWhiteSpace(profileAwareResult))
        {
            return profileAwareResult;
        }

        var packageJson = Path.Combine(rootDirectory, "package.json");
        var solution = Path.Combine(rootDirectory, "backend", "Dragon.Backend.slnx");

        if (File.Exists(solution))
        {
            var result = commandRunner(ResolveCommand("dotnet"), BuildDotnetTestArguments(rootDirectory, job, solution), rootDirectory);
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

    private static string BuildDotnetTestArguments(string rootDirectory, SelfBuildJob job, string solution)
    {
        var artifactRoot = Path.Combine(
            rootDirectory,
            ".dragon",
            "artifacts",
            "dotnet-test",
            $"{job.Issue}-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        Directory.CreateDirectory(artifactRoot);

        return string.Join(
            " ",
            [
                "test",
                QuoteArgument(solution),
                "--artifacts-path",
                QuoteArgument(artifactRoot),
                "--property:UseSharedCompilation=false"
            ]);
    }

    private static string QuoteArgument(string value) => $"\"{value}\"";

    private static string? TryExecuteProfileAwareTest(string rootDirectory, SelfBuildJob job)
    {
        if (IsBackendStackImplementationProfile(job))
        {
            var profileRoot = ResolveBackendStackProfileRoot(rootDirectory, job);
            if (profileRoot is null)
            {
                return null;
            }

            var composePath = Path.Combine(profileRoot, "docker-compose.yml");
            var smokePath = Path.Combine(profileRoot, "tests", "compose-smoke.sh");
            var readinessPath = Path.Combine(profileRoot, "tests", "stack-readiness.json");

            if (!File.Exists(composePath))
            {
                throw new InvalidOperationException($"Backend stack validation failed because {Path.GetRelativePath(rootDirectory, composePath)} does not exist.");
            }

            if (!File.Exists(smokePath))
            {
                throw new InvalidOperationException($"Backend stack validation failed because {Path.GetRelativePath(rootDirectory, smokePath)} does not exist.");
            }

            if (!File.Exists(readinessPath))
            {
                throw new InvalidOperationException($"Backend stack validation failed because {Path.GetRelativePath(rootDirectory, readinessPath)} does not exist.");
            }

            using var readinessDocument = JsonDocument.Parse(File.ReadAllText(readinessPath));
            if (readinessDocument.RootElement.ValueKind != JsonValueKind.Object ||
                !readinessDocument.RootElement.TryGetProperty("status", out var statusProperty) ||
                string.IsNullOrWhiteSpace(statusProperty.GetString()))
            {
                throw new InvalidOperationException($"Backend stack validation failed because {Path.GetRelativePath(rootDirectory, readinessPath)} has no status field.");
            }

            return $"Validated backend stack smoke assets in {Path.GetRelativePath(rootDirectory, profileRoot)}.";
        }

        if (!IsDotnetSliceImplementationProfile(job))
        {
            return null;
        }

        var dotnetProfileRoot = ResolveDotnetProfileRoot(rootDirectory, job);
        if (dotnetProfileRoot is null)
        {
            return null;
        }

        var implementationProfile = job.Metadata.GetValueOrDefault("implementationProfile");
        if (string.Equals(implementationProfile, "dotnet/api", StringComparison.OrdinalIgnoreCase))
        {
            ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "Dragon.Api.sln"), ".NET API validation failed because");
            ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "tests", "Dragon.Api.Tests.csproj"), ".NET API validation failed because");
            ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "tests", "HealthEndpointTests.cs"), ".NET API validation failed because");
            return $"Validated .NET API slice test assets in {Path.GetRelativePath(rootDirectory, dotnetProfileRoot)}.";
        }

        ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "Dragon.Worker.sln"), ".NET worker validation failed because");
        ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "tests", "Dragon.Worker.Tests.csproj"), ".NET worker validation failed because");
        ValidateRequiredNonEmptyFile(rootDirectory, Path.Combine(dotnetProfileRoot, "tests", "WorkerOptionsTests.cs"), ".NET worker validation failed because");
        return $"Validated .NET worker slice test assets in {Path.GetRelativePath(rootDirectory, dotnetProfileRoot)}.";
    }

    private static bool IsBackendStackImplementationProfile(SelfBuildJob job) =>
        string.Equals(job.Metadata.GetValueOrDefault("implementationProfile"), "backend-stack/pi-autonomous-engine", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(job.Metadata.GetValueOrDefault("implementationProfile"), "backend-stack/pi-lite-engine", StringComparison.OrdinalIgnoreCase);

    private static bool IsDotnetSliceImplementationProfile(SelfBuildJob job) =>
        string.Equals(job.Metadata.GetValueOrDefault("implementationProfile"), "dotnet/api", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(job.Metadata.GetValueOrDefault("implementationProfile"), "dotnet/worker", StringComparison.OrdinalIgnoreCase);

    private static void ValidateRequiredNonEmptyFile(string rootDirectory, string fullPath, string messagePrefix)
    {
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"{messagePrefix} {Path.GetRelativePath(rootDirectory, fullPath)} does not exist.");
        }

        if (string.IsNullOrWhiteSpace(File.ReadAllText(fullPath)))
        {
            throw new InvalidOperationException($"{messagePrefix} {Path.GetRelativePath(rootDirectory, fullPath)} is empty.");
        }
    }

    private static string? ResolveBackendStackProfileRoot(string rootDirectory, SelfBuildJob job) =>
        ResolveProfileRoot(rootDirectory, job, "templates/repo-templates/backend-stack/", 4);

    private static string? ResolveDotnetProfileRoot(string rootDirectory, SelfBuildJob job) =>
        ResolveProfileRoot(rootDirectory, job, "templates/repo-templates/dotnet/", 4);

    private static string? ResolveProfileRoot(string rootDirectory, SelfBuildJob job, string prefix, int segmentsToKeep)
    {
        var targetArtifact = job.Metadata.GetValueOrDefault("targetArtifact");
        if (string.IsNullOrWhiteSpace(targetArtifact))
        {
            return null;
        }

        var normalizedArtifact = targetArtifact.Replace('\\', '/');
        var prefixIndex = normalizedArtifact.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0)
        {
            return null;
        }

        var relativeArtifact = normalizedArtifact[prefixIndex..];
        var segments = relativeArtifact.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < segmentsToKeep)
        {
            return null;
        }

        var profileRootRelative = string.Join(Path.DirectorySeparatorChar, segments.Take(segmentsToKeep));
        return Path.Combine(rootDirectory, profileRootRelative);
    }

    private static IssueWorkflowState? LoadWorkflowState(string rootDirectory, int issueNumber)
    {
        var store = new WorkflowStateStore(rootDirectory);
        return store.ReadAll().TryGetValue(issueNumber, out var workflow) ? workflow : null;
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
            var details = BuildCommandFailureDetails(result);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(details)
                    ? $"{commandDescription} failed."
                    : $"{commandDescription} failed: {details}");
        }
    }

    private static string BuildCommandFailureDetails(CommandResult result)
    {
        var stderr = result.StandardError.Trim();
        var stdout = result.StandardOutput.Trim();

        if (!string.IsNullOrWhiteSpace(stderr) && !string.IsNullOrWhiteSpace(stdout) &&
            !string.Equals(stderr, stdout, StringComparison.Ordinal))
        {
            return $"stderr: {stderr}{Environment.NewLine}stdout: {stdout}";
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            return stderr;
        }

        return stdout;
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

    private static CommandResult RunCommandWithInput(string fileName, string arguments, string workingDirectory, string? standardInput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        if (!string.IsNullOrWhiteSpace(standardInput))
        {
            process.StandardInput.Write(standardInput);
        }

        process.StandardInput.Close();
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

    private static bool ShouldUseCliFallback(AgentModelProviderException exception) =>
        exception.StatusCode == HttpStatusCode.TooManyRequests;

    private static string BuildCodexPrompt(AgentModelRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Execute this agent task through the local Codex CLI in read-only mode.");
        builder.AppendLine("Return only the final response content for the agent.");
        builder.AppendLine();
        builder.AppendLine($"Agent: {request.Agent}");
        builder.AppendLine($"Purpose: {request.Purpose}");
        builder.AppendLine($"Model hint: {request.Model}");
        builder.AppendLine($"Background mode: {request.Background}");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            builder.AppendLine("Instructions:");
            builder.AppendLine(request.Instructions);
            builder.AppendLine();
        }

        if (request.Metadata is { Count: > 0 })
        {
            builder.AppendLine("Metadata:");
            foreach (var pair in request.Metadata.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"- {pair.Key}: {pair.Value}");
            }

            builder.AppendLine();
        }

        builder.AppendLine("Messages:");
        foreach (var message in request.Messages)
        {
            builder.AppendLine($"[{message.Role}]");
            builder.AppendLine(message.Content);
            builder.AppendLine();
        }

        return builder.ToString().Trim();
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
