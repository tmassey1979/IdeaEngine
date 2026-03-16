using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class SelfBuildLoop
{
    private readonly QueueStore queueStore;
    private readonly WorkflowStateStore workflowStateStore;
    private readonly ExecutionRecordStore executionRecordStore;
    private readonly LocalJobExecutor jobExecutor;
    private readonly GithubIssueService githubIssueService;

    public SelfBuildLoop(
        string rootDirectory,
        string queueName = "dragon.jobs",
        GithubIssueService? githubIssueService = null,
        LocalJobExecutor? jobExecutor = null)
    {
        RootDirectory = rootDirectory;
        queueStore = new QueueStore(rootDirectory, queueName);
        workflowStateStore = new WorkflowStateStore(rootDirectory);
        executionRecordStore = new ExecutionRecordStore(rootDirectory);
        this.jobExecutor = jobExecutor ?? new LocalJobExecutor();
        this.githubIssueService = githubIssueService ?? new GithubIssueService();
    }

    public string RootDirectory { get; }

    public IReadOnlyList<SelfBuildJob> ReadQueue() => queueStore.ReadAll();

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

        RemoveSupersededRecoveryJobs(issues);

        if (queueStore.ReadAll().Count == 0)
        {
            var seeded = SeedNext(issues, repo, project);
            return new CycleResult("seed", seeded, null, []);
        }

        var job = queueStore.Dequeue()!;
        var execution = jobExecutor.Execute(RootDirectory, job);
        var workflow = workflowStateStore.Update(job, execution);
        var followUps = PublishFollowUps(job, execution);
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
            .Where(issue => issue.Labels.Contains("story", StringComparer.OrdinalIgnoreCase))
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

    private IReadOnlyList<SelfBuildJob> PublishFollowUps(SelfBuildJob job, JobExecutionResult execution)
    {
        if (!string.Equals(execution.Status, "success", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.Agent, "review", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(job.Agent, "test", StringComparison.OrdinalIgnoreCase) ||
            !IsImplementationAction(job.Action))
        {
            return [];
        }

        var followUps = new[]
        {
            CreateFollowUpJob(job, execution, "review", "review_issue", execution.JobId),
            CreateFollowUpJob(job, execution, "test", "test_issue", execution.JobId)
        };

        foreach (var followUp in followUps)
        {
            queueStore.Enqueue(followUp);
        }

        return followUps;
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

    private static SelfBuildJob CreateFollowUpJob(SelfBuildJob sourceJob, JobExecutionResult execution, string agent, string action, string parentJobId)
    {
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
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedBy"] = "system",
                ["source"] = "dragon-orchestrator-dotnet",
                ["parentJobId"] = parentJobId,
                ["parentIssue"] = sourceJob.Issue.ToString(),
                ["changedPaths"] = string.Join("|", execution.ChangedPaths ?? [])
            }
        );
    }

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

        if (title.Contains("architect", StringComparison.Ordinal))
        {
            return "architect";
        }

        if (title.Contains("refactor", StringComparison.Ordinal))
        {
            return "refactor";
        }

        return "developer";
    }

    private static bool IsRecoveryIssue(GithubIssue issue) =>
        issue.Labels.Contains("recovery", StringComparer.OrdinalIgnoreCase) ||
        issue.Title.Contains("[Recovery]", StringComparison.OrdinalIgnoreCase);

    private static bool IsImplementationAction(string action) =>
        string.Equals(action, "implement_issue", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(action, "recover_issue", StringComparison.OrdinalIgnoreCase);

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
