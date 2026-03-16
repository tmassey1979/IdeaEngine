using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class SelfBuildLoop
{
    private readonly QueueStore queueStore;
    private readonly WorkflowStateStore workflowStateStore;
    private readonly LocalJobExecutor jobExecutor;

    public SelfBuildLoop(string rootDirectory, string queueName = "dragon.jobs")
    {
        RootDirectory = rootDirectory;
        queueStore = new QueueStore(rootDirectory, queueName);
        workflowStateStore = new WorkflowStateStore(rootDirectory);
        jobExecutor = new LocalJobExecutor();
    }

    public string RootDirectory { get; }

    public IReadOnlyList<SelfBuildJob> ReadQueue() => queueStore.ReadAll();

    public SelfBuildJob SeedNext(IReadOnlyList<GithubIssue> issues, string repo = "IdeaEngine", string project = "DragonIdeaEngine")
    {
        var nextIssue = issues
            .Where(issue => issue.Labels.Contains("story"))
            .OrderBy(issue => issue.Number)
            .First();

        var agent = RecommendAgent(nextIssue);
        var job = SelfBuildJobFactory.Create(nextIssue, agent, repo, project);
        queueStore.Enqueue(job);
        return job;
    }

    public CycleResult CycleOnce(IReadOnlyList<GithubIssue> issues, string repo = "IdeaEngine", string project = "DragonIdeaEngine")
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

        return new CycleResult("consume", job, execution, followUps, workflow);
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
                ["parentIssue"] = sourceJob.Issue.ToString()
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
    IssueWorkflowState? Workflow = null
);
