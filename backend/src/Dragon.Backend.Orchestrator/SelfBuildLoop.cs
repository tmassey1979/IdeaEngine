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
        var workflows = workflowStateStore.ReadAll();
        var nextIssue = issues
            .Where(issue => issue.Labels.Contains("story"))
            .Where(issue => !workflows.TryGetValue(issue.Number, out var workflow) ||
                !string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
            .OrderBy(issue => issue.Number)
            .First();

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
        if (queueStore.ReadAll().Count == 0)
        {
            var seeded = SeedNext(issues, repo, project);
            return new CycleResult("seed", seeded, null, []);
        }

        var job = queueStore.Dequeue()!;
        var execution = jobExecutor.Execute(RootDirectory, job);
        var workflow = workflowStateStore.Update(job.Issue, job.Payload.Title, job.Agent, execution);
        var followUps = PublishFollowUps(job, execution);
        var executionRecord = executionRecordStore.Append(job, execution, followUps);
        var failureDisposition = ApplyFailurePolicy(job.Issue, workflow);
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

    private IReadOnlyList<SelfBuildJob> PublishFollowUps(SelfBuildJob job, JobExecutionResult execution)
    {
        if (!string.Equals(job.Agent, "developer", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(execution.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var followUps = new[]
        {
            CreateFollowUpJob(job, "review", "review_issue", execution.JobId),
            CreateFollowUpJob(job, "test", "test_issue", execution.JobId)
        };

        foreach (var followUp in followUps)
        {
            queueStore.Enqueue(followUp);
        }

        return followUps;
    }

    private static SelfBuildJob CreateFollowUpJob(SelfBuildJob sourceJob, string agent, string action, string parentJobId)
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
                ["changedPaths"] = string.Join("|", sourceJob.Payload.Operations?.Select(operation => operation.Path) ?? [])
            }
        );
    }

    private static string RecommendAgent(GithubIssue issue)
    {
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
