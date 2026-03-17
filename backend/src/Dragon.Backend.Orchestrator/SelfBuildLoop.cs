using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class SelfBuildLoop
{
    private static readonly JsonSerializerOptions StatusSerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly QueueStore queueStore;
    private readonly WorkflowStateStore workflowStateStore;
    private readonly ExecutionRecordStore executionRecordStore;
    private readonly LocalJobExecutor jobExecutor;
    private readonly GithubIssueService githubIssueService;

    public SelfBuildLoop(
        string rootDirectory,
        string queueName = "dragon.jobs",
        GithubIssueService? githubIssueService = null,
        LocalJobExecutor? jobExecutor = null,
        Func<string, string?>? environmentReader = null)
    {
        RootDirectory = rootDirectory;
        queueStore = new QueueStore(rootDirectory, queueName);
        workflowStateStore = new WorkflowStateStore(rootDirectory);
        executionRecordStore = new ExecutionRecordStore(rootDirectory);
        this.jobExecutor = jobExecutor ?? LocalJobExecutor.CreateDefault(environmentReader ?? Environment.GetEnvironmentVariable);
        this.githubIssueService = githubIssueService ?? new GithubIssueService();
    }

    public string RootDirectory { get; }

    public IReadOnlyList<SelfBuildJob> ReadQueue() => queueStore.ReadAll();

    public StatusSnapshot ReadStatus()
    {
        var queuedJobs = queueStore.ReadAll();
        var issues = workflowStateStore.ReadAll().Values
            .OrderBy(item => item.IssueNumber)
            .Select(workflow =>
            {
                var latestExecution = executionRecordStore.Read(workflow.IssueNumber)
                    .OrderByDescending(record => record.RecordedAt)
                    .FirstOrDefault();

                return new IssueStatusSnapshot(
                    workflow.IssueNumber,
                    workflow.IssueTitle,
                    workflow.OverallStatus,
                    InferCurrentStage(workflow),
                    queuedJobs.Count(job => job.Issue == workflow.IssueNumber),
                    workflow.Note,
                    latestExecution?.Summary,
                    latestExecution?.Notes,
                    latestExecution?.RecordedAt
                );
            })
            .ToArray();

        return new StatusSnapshot(queuedJobs.Count, issues);
    }

    public StatusSnapshot WriteStatus(string outputPath)
    {
        var snapshot = ReadStatus();
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(snapshot, StatusSerializerOptions));
        return snapshot;
    }

    public IReadOnlyList<GithubIssue> LoadGithubIssues(string owner, string repo) =>
        githubIssueService.ListStoryIssues(owner, repo, RootDirectory);

    public SelfBuildJob SeedNext(IReadOnlyList<GithubIssue> issues, string repo = "IdeaEngine", string project = "DragonIdeaEngine")
    {
        var nextIssue = SelectNextIssue(issues);

        var agent = RecommendAgent(nextIssue);
        var job = SelfBuildJobFactory.Create(nextIssue, agent, repo, project);
        queueStore.Enqueue(job);
        return job;
    }

    public CycleResult CycleOnce(
        IReadOnlyList<GithubIssue> issues,
        string repo = "IdeaEngine",
        string project = "DragonIdeaEngine",
        string? githubOwner = null,
        bool syncValidatedWorkflows = false)
    {
        var resumedParent = ResumeReleasedParent(issues, repo, project);
        if (resumedParent is not null)
        {
            return resumedParent;
        }

        var stalledWorkflow = SweepStalledWorkflow(repo, githubOwner, syncValidatedWorkflows);
        if (stalledWorkflow is not null)
        {
            return stalledWorkflow;
        }

        var competingImplementationArtifacts = GetCompetingImplementationArtifacts();
        RemoveSupersededRecoveryJobs(issues);
        RemoveSupersededSummaryJobs();
        RemoveSupersededImplementationJobs();

        if (queueStore.ReadAll().Count == 0)
        {
            var seeded = SeedNext(issues, repo, project);
            return new CycleResult("seed", seeded, null, []);
        }

        var job = queueStore.Dequeue()!;
        var execution = jobExecutor.Execute(RootDirectory, job);
        var workflow = workflowStateStore.Update(job, execution);
        var followUps = PublishFollowUps(job, execution, competingImplementationArtifacts);
        var executionRecord = executionRecordStore.Append(job, execution, followUps);
        var failureDisposition = ApplyFailurePolicy(job.Issue, workflow);
        if (failureDisposition?.Quarantined == true)
        {
            workflow = workflowStateStore.ReadAll()[job.Issue];
        }
        var githubSync = TrySyncWorkflow(githubOwner, repo, workflow, syncValidatedWorkflows);

        return new CycleResult("consume", job, execution, followUps, workflow, githubSync, executionRecord, failureDisposition);
    }

    public CycleResult CycleOnceFromGithub(
        string owner,
        string repo,
        string project = "DragonIdeaEngine",
        bool syncValidatedWorkflows = false)
    {
        var issues = LoadGithubIssues(owner, repo);
        return CycleOnce(issues, repo, project, owner, syncValidatedWorkflows);
    }

    public RunUntilIdleResult RunUntilIdle(
        IReadOnlyList<GithubIssue> issues,
        string repo = "IdeaEngine",
        string project = "DragonIdeaEngine",
        string? githubOwner = null,
        bool syncValidatedWorkflows = false,
        int maxCycles = 100)
    {
        var cycles = new List<CycleResult>();
        for (var index = 0; index < maxCycles; index += 1)
        {
            if (queueStore.ReadAll().Count == 0 && !HasSchedulableWork(issues))
            {
                return new RunUntilIdleResult(cycles, true, false);
            }

            cycles.Add(CycleOnce(issues, repo, project, githubOwner, syncValidatedWorkflows));
        }

        return new RunUntilIdleResult(cycles, false, true);
    }

    public RunUntilIdleResult RunUntilIdleFromGithub(
        string owner,
        string repo,
        string project = "DragonIdeaEngine",
        bool syncValidatedWorkflows = false,
        int maxCycles = 100)
    {
        var cycles = new List<CycleResult>();
        for (var index = 0; index < maxCycles; index += 1)
        {
            var issues = LoadGithubIssues(owner, repo);
            if (queueStore.ReadAll().Count == 0 && !HasSchedulableWork(issues))
            {
                return new RunUntilIdleResult(cycles, true, false);
            }

            cycles.Add(CycleOnce(issues, repo, project, owner, syncValidatedWorkflows));
        }

        return new RunUntilIdleResult(cycles, false, true);
    }

    public GithubSyncResult SyncValidatedWorkflow(string owner, string repo, int issueNumber)
    {
        var workflows = workflowStateStore.ReadAll();
        if (!workflows.TryGetValue(issueNumber, out var workflow))
        {
            return new GithubSyncResult(false, false, $"No workflow state found for issue #{issueNumber}.");
        }

        return githubIssueService.SyncWorkflow(owner, repo, workflow, executionRecordStore.Read(issueNumber), RootDirectory);
    }

    private GithubSyncResult? TrySyncWorkflow(string? githubOwner, string repo, IssueWorkflowState workflow, bool syncValidatedWorkflows)
    {
        if (!syncValidatedWorkflows || string.IsNullOrWhiteSpace(githubOwner))
        {
            return null;
        }

        return githubIssueService.SyncWorkflow(githubOwner, repo, workflow, executionRecordStore.Read(workflow.IssueNumber), RootDirectory);
    }

    private bool HasSchedulableWork(IReadOnlyList<GithubIssue> issues)
    {
        try
        {
            _ = SelectNextIssue(issues);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private GithubIssue SelectNextIssue(IReadOnlyList<GithubIssue> issues)
    {
        var workflows = workflowStateStore.ReadAll();
        var latestRecoveryIssuesByParent = issues
            .Where(IsRecoveryIssue)
            .Where(issue => issue.SourceIssueNumber is not null)
            .GroupBy(issue => issue.SourceIssueNumber!.Value)
            .ToDictionary(group => group.Key, group => group.MaxBy(issue => issue.Number)!.Number);

        return issues
            .Where(IsSchedulableStoryIssue)
            .Where(issue => !workflows.TryGetValue(issue.Number, out var workflow) || (
                !string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase) &&
                !(workflow.ActiveRecoveryIssueNumbers?.Any() ?? false)
            ))
            .Where(issue => !IsRecoveryIssue(issue) ||
                issue.SourceIssueNumber is null ||
                latestRecoveryIssuesByParent.TryGetValue(issue.SourceIssueNumber.Value, out var latestIssueNumber) && latestIssueNumber == issue.Number)
            .OrderByDescending(issue => ShouldPrioritizeRecoveryIssue(issue, workflows))
            .ThenBy(issue => issue.Number)
            .First();
    }

    private FailureDisposition? ApplyFailurePolicy(int issueNumber, IssueWorkflowState workflow)
    {
        if (!string.Equals(workflow.OverallStatus, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var disposition = FailurePolicy.Evaluate(executionRecordStore.Read(issueNumber));
        if (!disposition.Quarantined)
        {
            return disposition;
        }

        workflowStateStore.OverrideOverallStatus(issueNumber, "quarantined", disposition.Reason!);
        queueStore.RemoveAll(job => job.Issue == issueNumber);
        return disposition;
    }

    private CycleResult? SweepStalledWorkflow(string repo, string? githubOwner, bool syncValidatedWorkflows)
    {
        foreach (var workflow in workflowStateStore.ReadAll().Values.OrderBy(item => item.IssueNumber))
        {
            var disposition = FailurePolicy.Evaluate(workflow);
            if (!disposition.Quarantined)
            {
                continue;
            }

            var updatedWorkflow = workflowStateStore.OverrideOverallStatus(workflow.IssueNumber, "quarantined", disposition.Reason!);
            queueStore.RemoveAll(job => job.Issue == workflow.IssueNumber);
            var githubSync = TrySyncWorkflow(githubOwner, repo, updatedWorkflow, syncValidatedWorkflows);
            return new CycleResult("quarantine", null, null, [], updatedWorkflow, githubSync, null, disposition);
        }

        return null;
    }

    private CycleResult? ResumeReleasedParent(
        IReadOnlyList<GithubIssue> issues,
        string repo,
        string project)
    {
        var queuedIssueNumbers = queueStore.ReadAll().Select(job => job.Issue).ToHashSet();
        foreach (var workflow in workflowStateStore.ReadAll().Values.OrderBy(item => item.IssueNumber))
        {
            if (!string.Equals(workflow.OverallStatus, "in_progress", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(workflow.Note, "Recovery child completed; parent returned to active flow.", StringComparison.Ordinal))
            {
                continue;
            }

            if (queuedIssueNumbers.Contains(workflow.IssueNumber))
            {
                continue;
            }

            var issue = issues.FirstOrDefault(candidate => candidate.Number == workflow.IssueNumber);
            if (issue is null)
            {
                continue;
            }

            var agent = RecommendAgent(issue);
            var job = SelfBuildJobFactory.Create(issue, agent, repo, project);
            queueStore.Enqueue(job);
            var updatedWorkflow = workflowStateStore.UpdateNote(workflow.IssueNumber, "Recovery child completed; parent requeued for active flow.");
            return new CycleResult("resume", job, null, [], updatedWorkflow);
        }

        return null;
    }

    private IReadOnlyList<SelfBuildJob> PublishFollowUps(
        SelfBuildJob job,
        JobExecutionResult execution,
        ISet<string>? competingImplementationArtifacts = null)
    {
        if (!string.Equals(execution.Status, "success", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.Agent, "review", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.Agent, "test", StringComparison.OrdinalIgnoreCase) ||
            !IsImplementationAction(job.Action))
        {
            return [];
        }

        var requestedFollowUps = execution.RequestedFollowUps ?? [];
        var followUps = new List<SelfBuildJob>
        {
            CreateFollowUpJob(job, execution, "review", "review_issue", execution.JobId),
            CreateFollowUpJob(job, execution, "test", "test_issue", execution.JobId)
        };

        EnqueueDeferredSummaryFollowUp(job, execution, followUps);
        EnqueueOperatorSummaryFollowUp(job, execution, followUps, competingImplementationArtifacts);

        foreach (var requestedFollowUp in requestedFollowUps
            .OrderBy(GetRequestedFollowUpPriorityRank)
            .ThenBy(GetRequestedFollowUpActionRank)
            .ThenBy(followUp => followUp.Agent ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(followUp => followUp.Action ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var followUpAgent = string.IsNullOrWhiteSpace(requestedFollowUp.Agent)
                ? InferPreferredAgent(requestedFollowUp.TargetArtifact)
                : requestedFollowUp.Agent;
            var followUpAction = string.IsNullOrWhiteSpace(requestedFollowUp.Action)
                ? InferFollowUpAction(requestedFollowUp)
                : requestedFollowUp.Action;

            if (string.IsNullOrWhiteSpace(followUpAgent) || string.IsNullOrWhiteSpace(followUpAction))
            {
                continue;
            }

            if (followUps.Any(existing =>
                string.Equals(existing.Agent, followUpAgent, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Action, followUpAction, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (IsRedundantSummaryFollowUp(followUps, followUpAgent, followUpAction, requestedFollowUp.TargetArtifact))
            {
                DeferSummaryFollowUp(followUps, followUpAgent, requestedFollowUp);
                continue;
            }

            followUps.Add(CreateFollowUpJob(
                job,
                execution,
                followUpAgent,
                followUpAction,
                execution.JobId,
                requestedFollowUp.Priority,
                requestedFollowUp.Reason,
                requestedFollowUp.Blocking,
                requestedFollowUp.TargetArtifact,
                requestedFollowUp.TargetOutcome));
        }

        foreach (var followUp in followUps)
        {
            queueStore.Enqueue(followUp);
        }

        return followUps;
    }

    private void EnqueueDeferredSummaryFollowUp(
        SelfBuildJob sourceJob,
        JobExecutionResult execution,
        List<SelfBuildJob> followUps)
    {
        if (!sourceJob.Metadata.TryGetValue("deferredSummaryAgent", out var deferredAgent) ||
            !sourceJob.Metadata.TryGetValue("deferredSummaryAction", out var deferredAction) ||
            string.IsNullOrWhiteSpace(deferredAgent) ||
            string.IsNullOrWhiteSpace(deferredAction))
        {
            return;
        }

        if (sourceJob.Metadata.TryGetValue("deferredSummaryTargetArtifact", out var deferredTargetArtifact) &&
            !string.IsNullOrWhiteSpace(deferredTargetArtifact) &&
            !(execution.ChangedPaths ?? []).Any(path =>
                string.Equals(path, deferredTargetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (sourceJob.Metadata.TryGetValue("deferredSummaryTargetArtifact", out deferredTargetArtifact) &&
            !string.IsNullOrWhiteSpace(deferredTargetArtifact) &&
            queueStore.ReadAll().Any(existing =>
                string.Equals(existing.Action, "implement_issue", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), deferredTargetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (followUps.Any(existing =>
            string.Equals(existing.Agent, deferredAgent, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Action, deferredAction, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        followUps.Add(CreateFollowUpJob(
            sourceJob,
            execution,
            deferredAgent,
            deferredAction,
            execution.JobId,
            sourceJob.Metadata.GetValueOrDefault("deferredSummaryPriority"),
            sourceJob.Metadata.GetValueOrDefault("deferredSummaryReason"),
            false,
            sourceJob.Metadata.GetValueOrDefault("deferredSummaryTargetArtifact"),
            sourceJob.Metadata.GetValueOrDefault("deferredSummaryTargetOutcome")));
    }

    private void EnqueueOperatorSummaryFollowUp(
        SelfBuildJob sourceJob,
        JobExecutionResult execution,
        List<SelfBuildJob> followUps,
        ISet<string>? competingImplementationArtifacts = null)
    {
        var targetArtifact = sourceJob.Metadata.GetValueOrDefault("targetArtifact");
        if (string.IsNullOrWhiteSpace(targetArtifact))
        {
            return;
        }

        if (competingImplementationArtifacts?.Contains(targetArtifact) == true)
        {
            return;
        }

        var changedPaths = (execution.ChangedPaths ?? [])
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (changedPaths.Length <= 1 ||
            !changedPaths.Any(path => string.Equals(path, targetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (queueStore.ReadAll().Any(existing =>
            string.Equals(existing.Action, "implement_issue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), targetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var summaryAgent = InferOperatorSummaryAgent(targetArtifact);
        var changedArtifactRollup = string.Join("|", changedPaths);
        if (followUps.Any(existing =>
            string.Equals(existing.Agent, summaryAgent, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Action, "summarize_issue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), targetArtifact, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        followUps.Add(CreateFollowUpJob(
            sourceJob,
            execution,
            summaryAgent,
            "summarize_issue",
            execution.JobId,
            "low",
            "Summarize the broader operator impact after the targeted implementation.",
            false,
            targetArtifact,
            "Summarize the broader operator impact of the targeted implementation.",
            ("changedArtifactRollup", changedArtifactRollup)));
    }

    private static string InferOperatorSummaryAgent(string targetArtifact)
    {
        var preferredAgent = InferPreferredAgent(targetArtifact);
        return string.Equals(preferredAgent, "documentation", StringComparison.OrdinalIgnoreCase)
            ? "documentation"
            : "feedback";
    }

    private static void DeferSummaryFollowUp(
        List<SelfBuildJob> followUps,
        string followUpAgent,
        RequestedFollowUp requestedFollowUp)
    {
        var index = followUps.FindIndex(existing =>
            string.Equals(existing.Agent, followUpAgent, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Action, "implement_issue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), requestedFollowUp.TargetArtifact, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return;
        }

        var implementationFollowUp = followUps[index];
        var metadata = new Dictionary<string, string>(implementationFollowUp.Metadata, StringComparer.Ordinal)
        {
            ["deferredSummaryAgent"] = followUpAgent,
            ["deferredSummaryAction"] = "summarize_issue"
        };

        if (!string.IsNullOrWhiteSpace(requestedFollowUp.Priority))
        {
            metadata["deferredSummaryPriority"] = requestedFollowUp.Priority;
        }

        if (!string.IsNullOrWhiteSpace(requestedFollowUp.Reason))
        {
            metadata["deferredSummaryReason"] = requestedFollowUp.Reason;
        }

        if (!string.IsNullOrWhiteSpace(requestedFollowUp.TargetArtifact))
        {
            metadata["deferredSummaryTargetArtifact"] = requestedFollowUp.TargetArtifact;
        }

        if (!string.IsNullOrWhiteSpace(requestedFollowUp.TargetOutcome))
        {
            metadata["deferredSummaryTargetOutcome"] = requestedFollowUp.TargetOutcome;
        }

        followUps[index] = implementationFollowUp with { Metadata = metadata };
    }

    private static bool IsRedundantSummaryFollowUp(
        IReadOnlyList<SelfBuildJob> existingFollowUps,
        string followUpAgent,
        string followUpAction,
        string? targetArtifact)
    {
        if (!string.Equals(followUpAction, "summarize_issue", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(targetArtifact))
        {
            return false;
        }

        return existingFollowUps.Any(existing =>
            string.Equals(existing.Agent, followUpAgent, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Action, "implement_issue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(existing.Metadata.GetValueOrDefault("targetArtifact"), targetArtifact, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetRequestedFollowUpPriorityRank(RequestedFollowUp followUp)
    {
        if (string.Equals(followUp.Priority, "high", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(followUp.Priority, "low", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    private static int GetRequestedFollowUpActionRank(RequestedFollowUp followUp)
    {
        var action = string.IsNullOrWhiteSpace(followUp.Action)
            ? InferFollowUpAction(followUp)
            : followUp.Action;

        return action?.ToLowerInvariant() switch
        {
            "implement_issue" => 0,
            "summarize_issue" => 1,
            _ => 2
        };
    }

    private void RemoveSupersededRecoveryJobs(IReadOnlyList<GithubIssue> issues)
    {
        var latestRecoveryIssuesByParent = issues
            .Where(IsRecoveryIssue)
            .Where(issue => issue.SourceIssueNumber is not null)
            .GroupBy(issue => issue.SourceIssueNumber!.Value)
            .ToDictionary(group => group.Key, group => group.MaxBy(issue => issue.Number)!.Number);

        queueStore.RemoveAll(job =>
        {
            if (!string.Equals(job.Metadata.GetValueOrDefault("workType"), "recovery", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!int.TryParse(job.Metadata.GetValueOrDefault("sourceIssueNumber"), out var sourceIssueNumber))
            {
                return false;
            }

            return latestRecoveryIssuesByParent.TryGetValue(sourceIssueNumber, out var latestRecoveryIssueNumber) &&
                latestRecoveryIssueNumber != job.Issue;
        });
    }

    private void RemoveSupersededSummaryJobs()
    {
        var jobs = queueStore.ReadAll().ToList();
        var queuedImplementationJobsByTarget = jobs
            .Where(job => string.Equals(job.Action, "implement_issue", StringComparison.OrdinalIgnoreCase))
            .Where(job => !string.IsNullOrWhiteSpace(job.Metadata.GetValueOrDefault("targetArtifact")))
            .GroupBy(job => job.Metadata.GetValueOrDefault("targetArtifact")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(job => GetImplementationPriorityRank(job))
                    .ThenByDescending(job => job.Issue)
                    .ThenByDescending(GetImplementationSpecificityRank)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        if (queuedImplementationJobsByTarget.Count == 0)
        {
            return;
        }

        var removalsByWinnerIssue = new Dictionary<int, List<int>>();
        var keptJobs = new List<SelfBuildJob>();
        foreach (var job in jobs)
        {
            if (!string.Equals(job.Action, "summarize_issue", StringComparison.OrdinalIgnoreCase))
            {
                keptJobs.Add(job);
                continue;
            }

            var targetArtifact = job.Metadata.GetValueOrDefault("targetArtifact");
            if (string.IsNullOrWhiteSpace(targetArtifact) ||
                !queuedImplementationJobsByTarget.TryGetValue(targetArtifact, out var winnerJob))
            {
                keptJobs.Add(job);
                continue;
            }

            if (!removalsByWinnerIssue.TryGetValue(winnerJob.Issue, out var removedIssues))
            {
                removedIssues = [];
                removalsByWinnerIssue[winnerJob.Issue] = removedIssues;
            }

            removedIssues.Add(job.Issue);
        }

        var updatedJobs = keptJobs
            .Select(job =>
            {
                if (!removalsByWinnerIssue.TryGetValue(job.Issue, out var removedIssues) || removedIssues.Count == 0)
                {
                    return job;
                }

                var metadata = new Dictionary<string, string>(job.Metadata, StringComparer.Ordinal)
                {
                    ["supersededSummaryIssues"] = string.Join("|", removedIssues.OrderBy(issue => issue)),
                    ["summaryConflictResolution"] = "Kept same-artifact implementation and pruned superseded summary jobs."
                };

                return job with { Metadata = metadata };
            })
            .ToList();

        queueStore.ReplaceAll(updatedJobs);
    }

    private void RemoveSupersededImplementationJobs()
    {
        var jobs = queueStore.ReadAll().ToList();
        var strongestPriorityByArtifact = jobs
            .Where(job => string.Equals(job.Action, "implement_issue", StringComparison.OrdinalIgnoreCase))
            .Where(job => !string.IsNullOrWhiteSpace(job.Metadata.GetValueOrDefault("targetArtifact")))
            .GroupBy(job => job.Metadata.GetValueOrDefault("targetArtifact")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(job => GetImplementationPriorityRank(job))
                    .ThenByDescending(job => job.Issue)
                    .ThenByDescending(GetImplementationSpecificityRank)
                    .First(),
                StringComparer.OrdinalIgnoreCase);

        if (strongestPriorityByArtifact.Count == 0)
        {
            return;
        }

        var removalsByWinnerIssue = new Dictionary<int, List<int>>();
        var keptJobs = new List<SelfBuildJob>();
        foreach (var job in jobs)
        {
            if (!string.Equals(job.Action, "implement_issue", StringComparison.OrdinalIgnoreCase))
            {
                keptJobs.Add(job);
                continue;
            }

            var targetArtifact = job.Metadata.GetValueOrDefault("targetArtifact");
            if (string.IsNullOrWhiteSpace(targetArtifact) ||
                !strongestPriorityByArtifact.TryGetValue(targetArtifact, out var strongestJob) ||
                AreEquivalentImplementationCandidates(job, strongestJob))
            {
                keptJobs.Add(job);
                continue;
            }

            if (job.Issue == strongestJob.Issue)
            {
                keptJobs.Add(job);
                continue;
            }

            if (!removalsByWinnerIssue.TryGetValue(strongestJob.Issue, out var removedIssues))
            {
                removedIssues = [];
                removalsByWinnerIssue[strongestJob.Issue] = removedIssues;
            }

            removedIssues.Add(job.Issue);
        }

        var updatedJobs = keptJobs
            .Select(job =>
            {
                if (!removalsByWinnerIssue.TryGetValue(job.Issue, out var removedIssues) || removedIssues.Count == 0)
                {
                    return job;
                }

                var metadata = new Dictionary<string, string>(job.Metadata, StringComparer.Ordinal)
                {
                    ["supersededImplementationIssues"] = string.Join("|", removedIssues.OrderBy(issue => issue)),
                    ["implementationConflictResolution"] = "Kept newer or higher-specificity same-artifact implementation; pruned weaker duplicates."
                };

                return job with { Metadata = metadata };
            })
            .ToList();

        queueStore.ReplaceAll(updatedJobs);
    }

    private HashSet<string> GetCompetingImplementationArtifacts() =>
        queueStore.ReadAll()
            .Where(job => string.Equals(job.Action, "implement_issue", StringComparison.OrdinalIgnoreCase))
            .Select(job => job.Metadata.GetValueOrDefault("targetArtifact"))
            .Where(targetArtifact => !string.IsNullOrWhiteSpace(targetArtifact))
            .GroupBy(targetArtifact => targetArtifact!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static int GetImplementationPriorityRank(SelfBuildJob job)
    {
        var priority = job.Metadata.GetValueOrDefault("requestedPriority");
        if (string.Equals(priority, "high", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (string.Equals(priority, "low", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return 1;
    }

    private static int GetImplementationSpecificityRank(SelfBuildJob job)
    {
        var outcome = job.Metadata.GetValueOrDefault("targetOutcome");
        if (string.IsNullOrWhiteSpace(outcome))
        {
            return 0;
        }

        return outcome.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    private static bool AreEquivalentImplementationCandidates(SelfBuildJob left, SelfBuildJob right) =>
        string.Equals(left.Agent, right.Agent, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Action, right.Action, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Metadata.GetValueOrDefault("requestedPriority"), right.Metadata.GetValueOrDefault("requestedPriority"), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Metadata.GetValueOrDefault("targetArtifact"), right.Metadata.GetValueOrDefault("targetArtifact"), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Metadata.GetValueOrDefault("targetOutcome"), right.Metadata.GetValueOrDefault("targetOutcome"), StringComparison.OrdinalIgnoreCase);

    private static SelfBuildJob CreateFollowUpJob(
        SelfBuildJob sourceJob,
        JobExecutionResult execution,
        string agent,
        string action,
        string parentJobId,
        string? requestedPriority = null,
        string? requestedReason = null,
        bool requestedBlocking = false,
        string? targetArtifact = null,
        string? targetOutcome = null,
        params (string Key, string Value)[] additionalMetadata)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["requestedBy"] = "system",
            ["source"] = "dragon-orchestrator-dotnet",
            ["parentJobId"] = parentJobId,
            ["parentIssue"] = sourceJob.Issue.ToString(),
            ["changedPaths"] = string.Join("|", execution.ChangedPaths ?? [])
        };

        if (!string.IsNullOrWhiteSpace(requestedPriority))
        {
            metadata["requestedPriority"] = requestedPriority;
        }

        if (!string.IsNullOrWhiteSpace(requestedReason))
        {
            metadata["requestedReason"] = requestedReason;
        }

        if (requestedBlocking)
        {
            metadata["requestedBlocking"] = "true";
        }

        if (!string.IsNullOrWhiteSpace(targetArtifact))
        {
            metadata["targetArtifact"] = targetArtifact;
        }

        if (!string.IsNullOrWhiteSpace(targetOutcome))
        {
            metadata["targetOutcome"] = targetOutcome;
        }

        var preferredAgent = InferPreferredAgent(targetArtifact);
        if (!string.IsNullOrWhiteSpace(preferredAgent))
        {
            metadata["preferredAgent"] = preferredAgent;
        }

        foreach (var (key, value) in additionalMetadata)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                metadata[key] = value;
            }
        }

        return new SelfBuildJob(
            agent,
            action,
            sourceJob.Repo,
            sourceJob.Project,
            sourceJob.Issue,
            new SelfBuildJobPayload(
                sourceJob.Payload.Title,
                ["story"],
                sourceJob.Payload.Heading,
                sourceJob.Payload.SourceFile,
                null
            ),
            metadata
        );
    }

    private static string? InferPreferredAgent(string? targetArtifact)
    {
        if (string.IsNullOrWhiteSpace(targetArtifact))
        {
            return null;
        }

        if (targetArtifact.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) ||
            targetArtifact.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return "documentation";
        }

        if (targetArtifact.StartsWith("backend/", StringComparison.OrdinalIgnoreCase) ||
            targetArtifact.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
            targetArtifact.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
        {
            return "refactor";
        }

        return null;
    }

    private static string? InferFollowUpAction(RequestedFollowUp followUp)
    {
        var targetOutcome = followUp.TargetOutcome ?? string.Empty;

        if (ContainsImplementationVerb(targetOutcome))
        {
            return "implement_issue";
        }

        if (!string.IsNullOrWhiteSpace(followUp.TargetArtifact) || !string.IsNullOrWhiteSpace(followUp.TargetOutcome))
        {
            return "summarize_issue";
        }

        return null;
    }

    private static bool ContainsImplementationVerb(string value) =>
        value.Contains("update", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("improve", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("refactor", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("rewrite", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("edit", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("clean up", StringComparison.OrdinalIgnoreCase);

    private static string RecommendAgent(GithubIssue issue)
    {
        if (IsRecoveryIssue(issue))
        {
            return "developer";
        }

        var title = issue.Title.ToLowerInvariant();

        if (title.Contains("review", StringComparison.Ordinal))
        {
            return "review";
        }

        if (title.Contains("test", StringComparison.Ordinal))
        {
            return "test";
        }

        if (title.Contains("architect agent", StringComparison.Ordinal))
        {
            return "architect";
        }

        if (title.Contains("documentation agent", StringComparison.Ordinal))
        {
            return "documentation";
        }

        if (title.Contains("feedback agent", StringComparison.Ordinal))
        {
            return "feedback";
        }

        if (title.Contains("idea agent", StringComparison.Ordinal))
        {
            return "idea";
        }

        if (title.Contains("repository manager agent", StringComparison.Ordinal))
        {
            return "repository-manager";
        }

        if (title.Contains("refactor agent", StringComparison.Ordinal))
        {
            return "refactor";
        }

        return "developer";
    }

    private static bool IsRecoveryIssue(GithubIssue issue) =>
        issue.Labels.Contains("recovery", StringComparer.OrdinalIgnoreCase) ||
        issue.Title.Contains("[Recovery]", StringComparison.OrdinalIgnoreCase);

    private static bool IsSchedulableStoryIssue(GithubIssue issue) =>
        issue.Labels.Contains("story", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("validated", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("waiting-follow-up", StringComparer.OrdinalIgnoreCase) &&
        !issue.Labels.Contains("superseded", StringComparer.OrdinalIgnoreCase);

    private static bool IsImplementationAction(string action) =>
        string.Equals(action, "implement_issue", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(action, "recover_issue", StringComparison.OrdinalIgnoreCase);

    private static string InferCurrentStage(IssueWorkflowState workflow)
    {
        var latestObserved = workflow.Stages
            .OrderByDescending(stage => stage.Value.ObservedAt)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(latestObserved.Key) ? "unknown" : latestObserved.Key;
    }

    private static bool ShouldPrioritizeRecoveryIssue(
        GithubIssue issue,
        IReadOnlyDictionary<int, IssueWorkflowState> workflows)
    {
        if (!IsRecoveryIssue(issue))
        {
            return false;
        }

        if (issue.SourceIssueNumber is null)
        {
            return true;
        }

        if (!workflows.TryGetValue(issue.SourceIssueNumber.Value, out var parentWorkflow))
        {
            return true;
        }

        if (parentWorkflow.ActiveRecoveryIssueNumbers?.Contains(issue.Number) == true)
        {
            return true;
        }

        return string.Equals(parentWorkflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record CycleResult(
    string Mode,
    SelfBuildJob? Job,
    JobExecutionResult? Execution,
    IReadOnlyList<SelfBuildJob> FollowUps,
    IssueWorkflowState? Workflow = null,
    GithubSyncResult? GithubSync = null,
    ExecutionRecord? ExecutionRecord = null,
    FailureDisposition? FailureDisposition = null
);

public sealed record RunUntilIdleResult(
    IReadOnlyList<CycleResult> Cycles,
    bool ReachedIdle,
    bool ReachedMaxCycles
);

public sealed record StatusSnapshot(
    int QueuedJobs,
    IReadOnlyList<IssueStatusSnapshot> Issues
);

public sealed record IssueStatusSnapshot(
    int IssueNumber,
    string IssueTitle,
    string OverallStatus,
    string CurrentStage,
    int QueuedJobCount,
    string? WorkflowNote,
    string? LatestExecutionSummary,
    string? LatestExecutionNotes,
    DateTimeOffset? LatestExecutionRecordedAt
);
