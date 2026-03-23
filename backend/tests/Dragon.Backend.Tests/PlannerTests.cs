using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Dragon.Backend.Contracts;
using Dragon.Backend.Orchestrator;

namespace Dragon.Backend.Tests;

public sealed class PlannerTests
{
    [Fact]
    public void LoadBacklogIndex_MapsKnownStoryMetadata()
    {
        var index = BacklogIndexLoader.Load(FindRepoRoot());

        var metadata = index["[Story] Dragon Idea Engine Master Codex: Core System Principles"];

        Assert.Equal("Core System Principles", metadata.Heading);
        Assert.Contains("01-dragon-idea-engine-master-codex", metadata.SourceFile, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_AddsArchitectureUpdate_ForCorePrinciples()
    {
        var operations = DeveloperOperationPlanner.Plan(
            new GithubIssue(
                22,
                "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                "OPEN",
                ["story"]
            )
        );

        Assert.Equal("docs/ARCHITECTURE.md", operations[0].Path);
        Assert.Contains("Core System Principles", operations[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_UsesRegistryDocument_ForRegistryArchitecture()
    {
        var operations = DeveloperOperationPlanner.Plan(
            new GithubIssue(
                213,
                "[Story] AGENT CAPABILITY REGISTRY AND DISCOVERY: Registry Architecture",
                "OPEN",
                ["story"],
                "The registry coordinates agent capabilities.",
                "Registry Architecture",
                "codex/sections/12-agent-capability-registry-and-discovery.md"
            )
        );

        Assert.Equal("docs/AGENT_REGISTRY.md", operations[0].Path);
        Assert.Contains("Agent Registry", operations[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_UsesRepositoryStructureDocument_ForRepositoryStructureStory()
    {
        var operations = DeveloperOperationPlanner.Plan(
            new GithubIssue(
                103,
                "[Story] Dragon Idea Engine Master Codex: Repository Structure",
                "OPEN",
                ["story"],
                "Dragon Idea Engine should use a multi-repo workspace structure.",
                "Repository Structure",
                "codex/sections/01-dragon-idea-engine-master-codex.md"
            )
        );

        Assert.Equal("docs/generated/repository-structure-notes.md", operations[0].Path);
        Assert.Contains("Repository Structure", operations[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Plan_UsesRepositoryStructureDocument_ForRootRepoStory()
    {
        var operations = DeveloperOperationPlanner.Plan(
            new GithubIssue(
                104,
                "[Story] Dragon Idea Engine Master Codex: Root Repo",
                "OPEN",
                ["story"],
                "Structure:\nDragonIdeaEngine\n├─ docs\n├─ runner",
                "Root Repo",
                "codex/sections/01-dragon-idea-engine-master-codex.md"
            )
        );

        Assert.Equal("docs/generated/repository-structure-notes.md", operations[0].Path);
        Assert.Contains("Repository Structure", operations[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecuteDeveloper_DoesNotDuplicateIdenticalAppendTextOperations()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var operation = new DeveloperOperation(
            "append_text",
            "docs/SDK.md",
            """

            ## Developer Operations

            The developer agent supports bounded `write_file`, `append_text`, and `replace_text` operations for deterministic self-improvement tasks.
            """
        );
        var job = new SelfBuildJob(
            "developer",
            "implement",
            "IdeaEngine",
            "DragonIdeaEngine",
            999,
            new SelfBuildJobPayload(
                "[Story] Dragon Idea Engine Master Codex Addendum: Developer Agent",
                ["story"],
                "Developer Agent",
                "codex/sections/02-dragon-idea-engine-master-codex-addendum.md",
                [operation]
            ),
            new Dictionary<string, string>()
        );

        var first = executor.Execute(root, job);
        var second = executor.Execute(root, job);

        var content = File.ReadAllText(Path.Combine(root, "docs", "SDK.md"));
        Assert.Equal(1, CountOccurrences(content, "## Developer Operations"));
        Assert.Single(first.ChangedPaths!);
        Assert.Empty(second.ChangedPaths!);
    }

    [Fact]
    public void ReleaseQuarantinedIssues_RequeuesFailedTestStage()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        var now = DateTimeOffset.UtcNow;

        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", now, ["docs/ARCHITECTURE.md"]));
        store.Update(22, "Core", "review", new JobExecutionResult("job-review", "review", "success", "reviewed", now.AddSeconds(1)));
        store.Update(22, "Core", "test", new JobExecutionResult("job-test", "test", "failed", "sdk missing", now.AddSeconds(2)));
        store.OverrideOverallStatus(22, "quarantined", "Quarantined after repeated failed test executions.");

        var loop = new SelfBuildLoop(root);
        var result = loop.ReleaseQuarantinedIssues(
            [
                new GithubIssue(
                    22,
                    "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                    "OPEN",
                    ["story"],
                    "",
                    "Core System Principles",
                    "codex/sections/01-dragon-idea-engine-master-codex.md")
            ]);

        Assert.Equal(1, result.ReleasedCount);
        var released = Assert.Single(result.ReleasedIssues);
        Assert.Equal("test", released.Agent);
        Assert.Equal("test_issue", released.Action);

        var queued = Assert.Single(loop.ReadQueue());
        Assert.Equal("test", queued.Agent);
        Assert.Equal("test_issue", queued.Action);

        var workflow = store.ReadAll()[22];
        Assert.Equal("in_progress", workflow.OverallStatus);
        Assert.DoesNotContain("test", workflow.Stages.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Released from quarantine for retry at test", workflow.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseQuarantinedIssues_SkipsIssuesWithActiveRecoveryChildren()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        var now = DateTimeOffset.UtcNow;

        store.Update(22, "Core", "test", new JobExecutionResult("job-test", "test", "failed", "blocked", now));
        store.OverrideOverallStatus(22, "quarantined", "Quarantined after repeated failed test executions.");
        store.Update(122, "[Recovery] Issue #22", "developer", new JobExecutionResult("job-recovery", "developer", "failed", "still blocked", now.AddSeconds(1)), 22);

        var loop = new SelfBuildLoop(root);
        var result = loop.ReleaseQuarantinedIssues(
            [
                new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"]),
                new GithubIssue(122, "[Recovery] Issue #22: Core System Principles", "OPEN", ["story", "recovery"], SourceIssueNumber: 22)
            ]);

        Assert.Equal(0, result.ReleasedCount);
        Assert.Empty(loop.ReadQueue());
        Assert.Equal("quarantined", store.ReadAll()[22].OverallStatus);
    }

    [Fact]
    public void CreateDeveloperJob_IncludesPlannedOperations()
    {
        var index = BacklogIndexLoader.Load(FindRepoRoot());
        var metadata = index["[Story] Dragon Idea Engine Master Codex: Core System Principles"];
        var issue = new GithubIssue(
            22,
            "[Story] Dragon Idea Engine Master Codex: Core System Principles",
            "OPEN",
            ["story"],
            "",
            metadata.Heading,
            metadata.SourceFile
        );

        var job = SelfBuildJobFactory.Create(issue, "developer", "IdeaEngine", "DragonIdeaEngine");

        Assert.Equal("developer", job.Agent);
        Assert.NotNull(job.Payload.Operations);
        Assert.Equal("docs/ARCHITECTURE.md", job.Payload.Operations![0].Path);
        Assert.Equal("dragon-orchestrator-dotnet", job.Metadata["source"]);
        Assert.Equal("story", job.Metadata["workType"]);
    }

    [Fact]
    public void CreateRecoveryJob_UsesRecoveryActionAndMetadata()
    {
        var issue = new GithubIssue(
            999,
            "[Recovery] Issue #22: Core",
            "OPEN",
            ["story", "recovery", "backlog"],
            SourceIssueNumber: 22
        );

        var job = SelfBuildJobFactory.Create(issue, "developer", "IdeaEngine", "DragonIdeaEngine");

        Assert.Equal("developer", job.Agent);
        Assert.Equal("recover_issue", job.Action);
        Assert.Equal("recovery", job.Metadata["workType"]);
        Assert.Equal("22", job.Metadata["sourceIssueNumber"]);
    }

    [Fact]
    public void SeedNext_SkipsOpenValidatedStoriesAndChoosesRunnableStory()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var issues = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story", "waiting-follow-up"]),
            new GithubIssue(23, "[Story] Dragon Idea Engine Master Codex: System Architecture", "OPEN", ["story"])
        };

        var job = loop.SeedNext(issues);

        Assert.Equal(23, job.Issue);
    }

    [Fact]
    public void RunUntilIdle_TreatsValidatedOnlyStoryListAsIdle()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var issues = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story", "waiting-follow-up"])
        };

        var result = loop.RunUntilIdle(issues, maxCycles: 5);

        Assert.True(result.ReachedIdle);
        Assert.False(result.ReachedMaxCycles);
        Assert.Empty(result.Cycles);
    }

    [Fact]
    public void QueueStore_EnqueuesAndDequeuesJobsInOrder()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        var first = SelfBuildJobFactory.Create(
            new GithubIssue(1, "First", "OPEN", ["story"]),
            "developer",
            "IdeaEngine",
            "DragonIdeaEngine"
        );
        var second = SelfBuildJobFactory.Create(
            new GithubIssue(2, "Second", "OPEN", ["story"]),
            "developer",
            "IdeaEngine",
            "DragonIdeaEngine"
        );

        queue.Enqueue(first);
        queue.Enqueue(second);

        Assert.Equal(2, queue.ReadAll().Count);
        Assert.Equal(1, queue.Peek()!.Issue);
        Assert.Equal(2, queue.ReadAll().Count);
        Assert.Equal(1, queue.Dequeue()!.Issue);
        Assert.Equal(2, queue.Dequeue()!.Issue);
        Assert.Null(queue.Dequeue());
    }

    [Fact]
    public void QueueStore_PrioritizesInterventionEscalationSummariesAheadOfOrdinarySummaries()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("Issue 22", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["workType"] = "operator-escalation",
                ["interventionEscalation"] = "true",
                ["targetOutcome"] = "Summarize the persistent critical intervention target and the next operator action."
            }));
        queue.Enqueue(new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            23,
            new SelfBuildJobPayload("Issue 23", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["workType"] = "story",
                ["targetOutcome"] = "Summarize the broader operator impact."
            }));

        var next = queue.Peek();

        Assert.NotNull(next);
        Assert.Equal(22, next!.Issue);
        Assert.Equal("true", next.Metadata["interventionEscalation"]);
    }

    [Fact]
    public void ReadStatus_IncludesQueuedCountsAndLatestExecutionNotes()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            500,
            new SelfBuildJobPayload("[Story] Provider Notes", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "refresh provider notes summary",
                ["requestedPriority"] = "high",
                ["requestedBlocking"] = "true",
                ["workType"] = "story"
            }));

        var store = new WorkflowStateStore(root);
        store.Update(500, "Provider Notes", "documentation", new JobExecutionResult("job-1", "documentation", "success", "updated", DateTimeOffset.UtcNow));

        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "documentation",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                500,
                new SelfBuildJobPayload("[Story] Provider Notes", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["implementationConflictResolution"] = "Kept newer or higher-specificity same-artifact implementation; pruned weaker duplicates.",
                    ["supersededImplementationIssues"] = "499"
                }),
            new JobExecutionResult("job-1", "documentation", "success", "updated", DateTimeOffset.UtcNow),
            []);

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(workerMode: "watch", workerState: "waiting", pollIntervalSeconds: 30);

        Assert.Equal(1, status.QueuedJobs);
        Assert.NotNull(status.LeadJob);
        Assert.Equal(500, status.LeadJob!.IssueNumber);
        Assert.Equal("documentation", status.LeadJob.Agent);
        Assert.Equal("implement_issue", status.LeadJob.Action);
        Assert.Equal("docs/generated/provider-notes.md", status.LeadJob.TargetArtifact);
        Assert.Equal("refresh provider notes summary", status.LeadJob.TargetOutcome);
        Assert.Equal("high", status.LeadJob.Priority);
        Assert.True(status.LeadJob.Blocking);
        Assert.Equal("story", status.LeadJob.WorkType);
        Assert.NotNull(status.InterventionTarget);
        Assert.Equal("implementation", status.InterventionTarget!.Kind);
        Assert.Equal(500, status.InterventionTarget.IssueNumber);
        Assert.Equal("docs/generated/provider-notes.md", status.InterventionTarget.TargetArtifact);
        Assert.Equal("refresh provider notes summary", status.InterventionTarget.TargetOutcome);
        Assert.Contains("refresh provider notes summary", status.InterventionTarget.Summary, StringComparison.Ordinal);
        Assert.Equal("Waiting to advance the lead implementation target on the next pass.", status.WorkerActivity);
        var issue = Assert.Single(status.Issues);
        Assert.Equal(500, issue.IssueNumber);
        Assert.Equal(1, issue.QueuedJobCount);
        Assert.Equal("in_progress", issue.OverallStatus);
        Assert.Equal("documentation", issue.CurrentStage);
        Assert.Equal("updated", issue.LatestExecutionSummary);
        Assert.Contains("Superseded implementation issues: 499", issue.LatestExecutionNotes, StringComparison.Ordinal);
    }

    [Fact]
    public void ExecutionRecordStore_StoresInterventionEscalationAcknowledgmentInNotes()
    {
        var root = CreateTempRoot();
        var store = new ExecutionRecordStore(root);
        var signature = "github-replay-drift|22|500|500|backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs|Summarize the persistent critical intervention target and the next operator action.";

        store.Append(
            new SelfBuildJob(
                "feedback",
                "summarize_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["interventionEscalation"] = "true",
                    ["interventionSignature"] = signature,
                    ["workType"] = "operator-escalation"
                }),
            new JobExecutionResult("job-1", "feedback", "success", "summarized", DateTimeOffset.UtcNow),
            []);

        var record = Assert.Single(store.Read(22));

        Assert.Contains($"Intervention escalation acknowledged: {signature}.", record.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadStatus_UsesOperatorEscalationAsInterventionTarget()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("Core", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["workType"] = "operator-escalation",
                ["interventionEscalation"] = "true",
                ["targetArtifact"] = "backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs",
                ["targetOutcome"] = "Summarize the persistent critical intervention target and the next operator action."
            }));

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(workerMode: "watch", workerState: "waiting");

        Assert.NotNull(status.LeadJob);
        Assert.Equal("operator-escalation", status.LeadJob!.WorkType);
        Assert.NotNull(status.InterventionTarget);
        Assert.Equal("operator-escalation", status.InterventionTarget!.Kind);
        Assert.Equal(22, status.InterventionTarget.IssueNumber);
        Assert.Contains("Escalate issue #22", status.InterventionTarget.Summary, StringComparison.Ordinal);
        Assert.False(status.InterventionTarget.Acknowledged);
        Assert.Equal("healthy", status.Health);
        Assert.Contains("1 queued job", status.AttentionSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Waiting to prepare an operator-facing escalation summary on the next pass.", status.WorkerActivity);
        Assert.Equal("escalating", status.RecentLoopSignal.Mode);
        Assert.Contains("actively escalating operator follow-up", status.RecentLoopSignal.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadStatus_MarksInterventionTargetAsAcknowledgedWhenMatchingSummarySucceeded()
    {
        var root = CreateTempRoot();
        var signature = "operator-escalation|22|||backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs|Summarize the persistent critical intervention target and the next operator action.";
        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "feedback",
                "summarize_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["interventionEscalation"] = "true",
                    ["interventionSignature"] = signature,
                    ["workType"] = "operator-escalation"
                }),
            new JobExecutionResult("job-1", "feedback", "success", "summarized", DateTimeOffset.UtcNow),
            []);

        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("Core", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["workType"] = "operator-escalation",
                ["interventionEscalation"] = "true",
                ["targetArtifact"] = "backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs",
                ["targetOutcome"] = "Summarize the persistent critical intervention target and the next operator action."
            }));

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(workerMode: "watch", workerState: "waiting");

        Assert.NotNull(status.InterventionTarget);
        Assert.True(status.InterventionTarget!.Acknowledged);
        Assert.Equal(0, status.InterventionTarget.AcknowledgedStreak);
        Assert.Equal("attention", status.Health);
        Assert.Contains("acknowledged but unresolved", status.AttentionSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Waiting to continue tracking an already-acknowledged operator escalation on the next pass.", status.WorkerActivity);
        Assert.Equal("monitoring", status.RecentLoopSignal.Mode);
        Assert.Contains("tracking acknowledged operator escalation", status.RecentLoopSignal.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadStatus_TreatsHistoricalQuarantineWithoutQueuedWorkAsAttention()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(500, "Provider Notes", "documentation", new JobExecutionResult("job-1", "documentation", "success", "updated", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(500, "quarantined", "Quarantined after repeated failures.");

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(workerMode: "watch", workerState: "running");

        Assert.Equal("attention", status.Health);
        Assert.Contains("quarantined issue", status.AttentionSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadStatus_TreatsQuarantineWithQueuedRecoveryWorkAsBlocked()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "developer",
            "recover_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            500,
            new SelfBuildJobPayload("[Recovery] Provider Notes", ["story", "recovery"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workType"] = "recovery",
                ["requestedBlocking"] = "true"
            }));

        var store = new WorkflowStateStore(root);
        store.Update(500, "Provider Notes", "documentation", new JobExecutionResult("job-1", "documentation", "success", "updated", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(500, "quarantined", "Quarantined after repeated failures.");

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(workerMode: "watch", workerState: "running");

        Assert.Equal("blocked", status.Health);
        Assert.Contains("queued recovery work", status.AttentionSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadStatus_ReportsDelayedProviderRetryWindow_WhenNoReadyJobsExist()
    {
        var root = CreateTempRoot();
        var retryNotBefore = DateTimeOffset.UtcNow.AddHours(1);
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "architect",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Architect Agent", ["story"], "Architect Agent", "codex/sections/01-dragon-idea-engine-master-codex.md", null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retryNotBeforeUtc"] = retryNotBefore.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                ["workType"] = "story"
            }));

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(workerMode: "watch", workerState: "waiting");

        Assert.NotNull(status.LeadJob);
        Assert.Equal(22, status.LeadJob!.IssueNumber);
        Assert.True(status.LeadJob.Delayed);
        Assert.Equal(retryNotBefore, status.LeadJob.RetryNotBeforeUtc);
        Assert.Equal("attention", status.Health);
        Assert.Contains("Provider retry remains delayed", status.AttentionSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("delayed-provider-retry", status.NextWakeReason);
        Assert.Equal(retryNotBefore, status.NextDelayedRetryAt);
        Assert.Equal("alert", status.DelayedRetryUrgency);
        Assert.Equal($"Next delayed provider retry unlocks at {retryNotBefore:O}.", status.DelayedRetrySummary);
        Assert.Equal($"Waiting for provider retry window on issue #22 until {retryNotBefore:O}.", status.WorkerActivity);
    }

    [Fact]
    public void ReadStatus_TreatsShortDelayedProviderRetryAsHealthy()
    {
        var root = CreateTempRoot();
        var retryNotBefore = DateTimeOffset.UtcNow.AddMinutes(2);
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "architect",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Architect Agent", ["story"], "Architect Agent", "codex/sections/01-dragon-idea-engine-master-codex.md", null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retryNotBeforeUtc"] = retryNotBefore.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                ["workType"] = "story"
            }));

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(workerMode: "watch", workerState: "waiting");

        Assert.Equal("healthy", status.Health);
        Assert.Contains("Waiting for the next provider retry window", status.AttentionSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("delayed-provider-retry", status.NextWakeReason);
        Assert.Equal(retryNotBefore, status.NextDelayedRetryAt);
        Assert.Equal("normal", status.DelayedRetryUrgency);
    }

    [Fact]
    public void ReadStatus_TreatsQuarantineWithQueuedRecoveryChildWorkAsBlockedAndExposesLeadQuarantine()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "developer",
            "recover_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            500,
            new SelfBuildJobPayload("[Recovery] Provider Notes", ["story", "recovery"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceIssueNumber"] = "22",
                ["workType"] = "recovery",
                ["requestedBlocking"] = "true"
            }));

        var store = new WorkflowStateStore(root);
        store.Update(22, "Provider Notes", "documentation", new JobExecutionResult("job-parent", "documentation", "failed", "blocked", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(22, "quarantined", "Parent is quarantined.");
        store.Update(
            500,
            "[Recovery] Provider Notes",
            "developer",
            new JobExecutionResult("job-recovery", "developer", "success", "started", DateTimeOffset.UtcNow),
            sourceIssueNumber: 22);

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(workerMode: "watch", workerState: "running");

        Assert.Equal("blocked", status.Health);
        Assert.Contains("Lead recovery: issue #22 via recovery #500", status.AttentionSummary, StringComparison.Ordinal);
        Assert.Equal(1, status.Rollup.ActionableQuarantinedIssues);
        Assert.NotNull(status.LeadQuarantine);
        Assert.Equal(22, status.LeadQuarantine!.IssueNumber);
        Assert.Equal("Provider Notes", status.LeadQuarantine.IssueTitle);
        Assert.Equal(1, status.LeadQuarantine.QueuedRecoveryJobs);
        Assert.Equal(500, status.LeadQuarantine.RecoveryIssueNumber);
        Assert.Equal("[Recovery] Provider Notes", status.LeadQuarantine.RecoveryIssueTitle);
        Assert.Equal("recovery-active", status.LeadQuarantine.State);
        Assert.Contains("Recovery issue #500", status.LeadQuarantine.Summary, StringComparison.Ordinal);
        Assert.NotNull(status.InterventionTarget);
        Assert.Equal("recovery-work", status.InterventionTarget!.Kind);
        Assert.Equal(22, status.InterventionTarget.IssueNumber);
        Assert.Equal(500, status.InterventionTarget.RecoveryIssueNumber);
        Assert.Contains("Recovery issue #500", status.InterventionTarget.Summary, StringComparison.Ordinal);
        Assert.Equal("Draining queued recovery work before ordinary implementation.", status.WorkerActivity);
        Assert.Equal("blocked", status.RecentLoopSignal.Mode);
        Assert.Contains("issue #22 via recovery #500", status.RecentLoopSignal.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadStatus_AnnotatesLeadQuarantineWhenGithubSyncRetryIsPending()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "developer",
            "recover_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            500,
            new SelfBuildJobPayload("[Recovery] Provider Notes", ["story", "recovery"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceIssueNumber"] = "22",
                ["workType"] = "recovery",
                ["requestedBlocking"] = "true"
            }));

        var store = new WorkflowStateStore(root);
        store.Update(22, "Provider Notes", "documentation", new JobExecutionResult("job-parent", "documentation", "failed", "blocked", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(22, "quarantined", "Parent is quarantined.");
        store.Update(
            500,
            "[Recovery] Provider Notes",
            "developer",
            new JobExecutionResult("job-recovery", "developer", "success", "started", DateTimeOffset.UtcNow),
            sourceIssueNumber: 22);

        var statusDirectory = Path.Combine(root, ".dragon", "status");
        Directory.CreateDirectory(statusDirectory);
        File.WriteAllText(
            Path.Combine(statusDirectory, "pending-github-sync.json"),
            """
            [
              {
                "issueNumber": 500,
                "summary": "GitHub sync failed for recovery issue #500.",
                "recordedAt": "2026-03-23T12:00:00Z",
                "attemptCount": 2,
                "lastAttemptedAt": "2026-03-23T12:01:00Z",
                "nextRetryAt": "2026-03-23T12:01:30Z"
              }
            ]
            """);

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(workerMode: "watch", workerState: "running", pollIntervalSeconds: 30);

        Assert.NotNull(status.LeadQuarantine);
        Assert.Equal("sync-drift", status.LeadQuarantine!.State);
        Assert.Contains("GitHub updates for recovery #500", status.LeadQuarantine.Summary, StringComparison.Ordinal);
        Assert.NotNull(status.InterventionTarget);
        Assert.Equal("github-replay-drift", status.InterventionTarget!.Kind);
        Assert.Equal(22, status.InterventionTarget.IssueNumber);
        Assert.Equal(500, status.InterventionTarget.RecoveryIssueNumber);
        Assert.Equal(500, status.InterventionTarget.PendingGithubSyncIssueNumber);
        Assert.Contains("GitHub updates for recovery #500", status.InterventionTarget.Summary, StringComparison.Ordinal);
        Assert.Equal(DateTimeOffset.Parse("2026-03-23T12:00:00Z"), status.InterventionTarget.ObservedAt);
        Assert.Contains("old", status.InterventionTarget.AgeSummary, StringComparison.Ordinal);
        Assert.Equal("critical", status.InterventionTarget.Escalation);
        Assert.Contains("critical", status.InterventionEscalationNote, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Replaying pending GitHub updates before the next GitHub pass.", status.WorkerActivity);
        Assert.Contains("old", status.AttentionSummary, StringComparison.Ordinal);
        Assert.Contains("oldest writeback drift", status.RecentLoopSignal.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadStatus_PrioritizesOldestPendingRecoveryWritebackDriftAsLeadQuarantine()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "developer",
            "recover_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            500,
            new SelfBuildJobPayload("[Recovery] Provider Notes", ["story", "recovery"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceIssueNumber"] = "22",
                ["workType"] = "recovery",
                ["requestedBlocking"] = "true"
            }));
        queue.Enqueue(new SelfBuildJob(
            "developer",
            "recover_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            600,
            new SelfBuildJobPayload("[Recovery] Queue Store", ["story", "recovery"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceIssueNumber"] = "32",
                ["workType"] = "recovery",
                ["requestedBlocking"] = "true"
            }));

        var store = new WorkflowStateStore(root);
        store.Update(22, "Provider Notes", "documentation", new JobExecutionResult("job-parent-1", "documentation", "failed", "blocked", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(22, "quarantined", "Parent is quarantined.");
        store.Update(32, "Queue Store", "developer", new JobExecutionResult("job-parent-2", "developer", "failed", "blocked", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(32, "quarantined", "Parent is quarantined.");
        store.Update(500, "[Recovery] Provider Notes", "developer", new JobExecutionResult("job-recovery-1", "developer", "success", "started", DateTimeOffset.UtcNow), sourceIssueNumber: 22);
        store.Update(600, "[Recovery] Queue Store", "developer", new JobExecutionResult("job-recovery-2", "developer", "success", "started", DateTimeOffset.UtcNow), sourceIssueNumber: 32);

        var statusDirectory = Path.Combine(root, ".dragon", "status");
        Directory.CreateDirectory(statusDirectory);
        File.WriteAllText(
            Path.Combine(statusDirectory, "pending-github-sync.json"),
            """
            [
              {
                "issueNumber": 600,
                "summary": "GitHub sync failed for recovery issue #600.",
                "recordedAt": "2026-03-23T12:05:00Z",
                "attemptCount": 2,
                "lastAttemptedAt": "2026-03-23T12:06:00Z"
              },
              {
                "issueNumber": 500,
                "summary": "GitHub sync failed for recovery issue #500.",
                "recordedAt": "2026-03-23T12:00:00Z",
                "attemptCount": 3,
                "lastAttemptedAt": "2026-03-23T12:06:30Z"
              }
            ]
            """);

        var loop = new SelfBuildLoop(root);
        var status = loop.ReadStatus(pollIntervalSeconds: 30);

        Assert.NotNull(status.LeadQuarantine);
        Assert.Equal(22, status.LeadQuarantine!.IssueNumber);
        Assert.Equal(500, status.LeadQuarantine.RecoveryIssueNumber);
        Assert.Equal(DateTimeOffset.Parse("2026-03-23T12:00:00Z"), status.LeadQuarantine.OldestPendingGithubSyncAt);
    }

    [Fact]
    public void ReadStatus_IncludesLatestGithubSyncFailure()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var stories = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"], "", "Core System Principles", "codex/sections/01-dragon-idea-engine-master-codex.md"),
            new GithubIssue(23, "[Story] Dragon Idea Engine Master Codex: System Architecture", "OPEN", ["story"], "", "System Architecture", "codex/sections/01-dragon-idea-engine-master-codex.md")
        };

        var github = new GithubIssueService((arguments, _) =>
        {
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issues/22/comments", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("gh command failed: gh: Resource not accessible by personal access token (HTTP 403)");
            }

            return string.Empty;
        });

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(1, string.Empty, "forced failure"));
        var loop = new SelfBuildLoop(root, githubIssueService: github, jobExecutor: executor);

        for (var index = 0; index < 12; index += 1)
        {
            var cycle = loop.CycleOnce(stories, repo: "IdeaEngine", project: "DragonIdeaEngine", githubOwner: "tmassey1979", syncValidatedWorkflows: true);
            if (cycle.FailureDisposition?.Quarantined == true)
            {
                break;
            }
        }

        var status = loop.ReadStatus(workerMode: "watch", workerState: "waiting", pollIntervalSeconds: 30);

        Assert.NotNull(status.LatestGithubSync);
        Assert.Equal(22, status.LatestGithubSync!.IssueNumber);
        Assert.True(status.LatestGithubSync.Attempted);
        Assert.False(status.LatestGithubSync.Updated);
        Assert.Contains("GitHub sync failed", status.LatestGithubSync.Summary, StringComparison.Ordinal);
        Assert.Equal(1, status.PendingGithubSyncCount);
        var pending = Assert.Single(status.PendingGithubSync!);
        Assert.Equal(22, pending.IssueNumber);
        Assert.Contains("GitHub sync failed", pending.Summary, StringComparison.Ordinal);
        Assert.True(pending.AttemptCount >= 1);
        Assert.NotNull(pending.LastAttemptedAt);
        Assert.NotNull(pending.NextRetryAt);
        Assert.True(pending.NextRetryAt > pending.LastAttemptedAt);
        Assert.Contains("1 GitHub update is waiting for retry: issue #22", status.PendingGithubSyncSummary, StringComparison.Ordinal);
        Assert.Contains("old", status.PendingGithubSyncSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadStatus_ClearsPendingGithubSyncAfterSuccessfulRetry()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "review", new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "test", new JobExecutionResult("job-test", "test", "success", "done", DateTimeOffset.UtcNow));

        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "developer",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["changedPaths"] = "docs/ARCHITECTURE.md"
                }),
            new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow),
            []);

        var failOnce = true;
        var github = new GithubIssueService((arguments, _) =>
        {
            if (arguments.Contains("issue comment 22", StringComparison.Ordinal) && failOnce)
            {
                failOnce = false;
                throw new InvalidOperationException("gh command failed: gh: Resource not accessible by personal access token (HTTP 403)");
            }

            return string.Empty;
        });

        var loop = new SelfBuildLoop(root, githubIssueService: github);
        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);
        var failedStatus = loop.ReadStatus();
        Assert.Equal(1, failedStatus.PendingGithubSyncCount);

        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);
        var recoveredStatus = loop.ReadStatus();
        Assert.Equal(0, recoveredStatus.PendingGithubSyncCount);
        Assert.Empty(recoveredStatus.PendingGithubSync ?? []);
        Assert.NotNull(recoveredStatus.LatestGithubSync);
        Assert.True(recoveredStatus.LatestGithubSync!.Updated);
        Assert.Null(recoveredStatus.PendingGithubSyncSummary);
    }

    [Fact]
    public void ReadStatus_IncrementsPendingGithubSyncRetryAttemptMetadata()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "review", new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "test", new JobExecutionResult("job-test", "test", "success", "done", DateTimeOffset.UtcNow));

        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "developer",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["changedPaths"] = "docs/ARCHITECTURE.md"
                }),
            new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow),
            []);

        var github = new GithubIssueService((arguments, _) =>
        {
            if (arguments.Contains("issue comment 22", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("gh command failed: gh: Resource not accessible by personal access token (HTTP 403)");
            }

            return string.Empty;
        });

        var loop = new SelfBuildLoop(root, githubIssueService: github);

        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);

        var firstStatus = loop.ReadStatus();
        var firstPending = Assert.Single(firstStatus.PendingGithubSync!);

        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);
        var retriedStatus = loop.ReadStatus(workerMode: "watch", workerState: "waiting", pollIntervalSeconds: 30);
        var retriedPending = Assert.Single(retriedStatus.PendingGithubSync!);

        Assert.Equal(2, retriedPending.AttemptCount);
        Assert.Equal(firstPending.RecordedAt, retriedPending.RecordedAt);
        Assert.NotNull(retriedPending.LastAttemptedAt);
        Assert.True(retriedPending.LastAttemptedAt >= firstPending.LastAttemptedAt);
        Assert.NotNull(retriedPending.NextRetryAt);
        Assert.True(retriedPending.NextRetryAt > retriedPending.LastAttemptedAt);
    }

    [Fact]
    public void RunPollingFromGithub_ReplaysPendingGithubSyncBacklogBeforePass()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "planning"));
        File.WriteAllText(
            Path.Combine(root, "planning", "backlog.json"),
            """
            {
              "stories": []
            }
            """);
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "review", new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "test", new JobExecutionResult("job-test", "test", "success", "done", DateTimeOffset.UtcNow));

        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "developer",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["changedPaths"] = "docs/ARCHITECTURE.md"
                }),
            new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow),
            []);

        var failOnce = true;
        var github = new GithubIssueService((arguments, _) =>
        {
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issue comment 22", StringComparison.Ordinal) && failOnce)
            {
                failOnce = false;
                throw new InvalidOperationException("gh command failed: gh: Resource not accessible by personal access token (HTTP 403)");
            }

            return string.Empty;
        });

        var loop = new SelfBuildLoop(root, githubIssueService: github);
        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);
        Assert.Equal(1, loop.ReadStatus().PendingGithubSyncCount);

        var result = loop.RunPollingFromGithub(
            "tmassey1979",
            "IdeaEngine",
            syncValidatedWorkflows: true,
            maxPasses: 2,
            idlePassesBeforeStop: 1,
            maxCyclesPerPass: 1);

        Assert.True(result.ReachedIdleThreshold);
        Assert.False(result.ReachedMaxPasses);
        Assert.Equal(2, result.Passes.Count);
        Assert.False(result.Passes[0].ReachedMaxCycles);
        Assert.True(result.Passes[0].ReachedIdle);
        Assert.True(result.Passes[1].ReachedIdle);
        var recoveredStatus = loop.ReadStatus();
        Assert.Equal(0, recoveredStatus.PendingGithubSyncCount);
        Assert.Empty(recoveredStatus.PendingGithubSync ?? []);
        Assert.NotNull(recoveredStatus.LatestGithubSync);
        Assert.True(recoveredStatus.LatestGithubSync!.Updated);
    }

    [Fact]
    public void RunWatchingFromGithub_DoesNotTreatReplayOnlyPassAsIdleForShutdown()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "planning"));
        File.WriteAllText(
            Path.Combine(root, "planning", "backlog.json"),
            """
            {
              "stories": []
            }
            """);
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "review", new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "test", new JobExecutionResult("job-test", "test", "success", "done", DateTimeOffset.UtcNow));

        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "developer",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["changedPaths"] = "docs/ARCHITECTURE.md"
                }),
            new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow),
            []);

        var failOnce = true;
        var github = new GithubIssueService((arguments, _) =>
        {
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issue comment 22", StringComparison.Ordinal) && failOnce)
            {
                failOnce = false;
                throw new InvalidOperationException("gh command failed: gh: Resource not accessible by personal access token (HTTP 403)");
            }

            return string.Empty;
        });

        var loop = new SelfBuildLoop(root, githubIssueService: github);
        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);
        Assert.Equal(1, loop.ReadStatus().PendingGithubSyncCount);

        var pauses = new List<TimeSpan>();
        var result = loop.RunWatchingFromGithub(
            "tmassey1979",
            "IdeaEngine",
            TimeSpan.FromSeconds(15),
            syncValidatedWorkflows: true,
            maxPasses: 2,
            idlePassesBeforeStop: 1,
            maxCyclesPerPass: 1,
            delayAction: pauses.Add);

        Assert.True(result.ReachedIdleThreshold);
        Assert.False(result.ReachedMaxPasses);
        Assert.Equal(2, result.Passes.Count);
        Assert.Single(pauses);
    }

    [Fact]
    public void ReplayPendingGithubSyncs_WritesReplaySummaryIntoStatus()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "review", new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "test", new JobExecutionResult("job-test", "test", "success", "done", DateTimeOffset.UtcNow));

        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "developer",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["changedPaths"] = "docs/ARCHITECTURE.md"
                }),
            new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow),
            []);

        var failOnce = true;
        var github = new GithubIssueService((arguments, _) =>
        {
            if (arguments.Contains("issue comment 22", StringComparison.Ordinal) && failOnce)
            {
                failOnce = false;
                throw new InvalidOperationException("gh command failed: gh: Resource not accessible by personal access token (HTTP 403)");
            }

            return string.Empty;
        });

        var loop = new SelfBuildLoop(root, githubIssueService: github);
        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);
        Assert.Equal(1, loop.ReadStatus().PendingGithubSyncCount);

        var replayResults = loop.ReplayPendingGithubSyncs("tmassey1979", "IdeaEngine");

        Assert.Single(replayResults);
        var status = loop.ReadStatus();
        Assert.NotNull(status.LatestGithubReplay);
        Assert.Equal(1, status.LatestGithubReplay!.AttemptedCount);
        Assert.Equal(1, status.LatestGithubReplay.UpdatedCount);
        Assert.Equal(0, status.LatestGithubReplay.FailedCount);
        Assert.Contains("Replayed 1 pending GitHub update", status.LatestGithubReplay.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncValidatedWorkflow_DefersHeartbeatDuringLongProviderBackoff()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, ".dragon", "status"));
        File.WriteAllText(
            Path.Combine(root, ".dragon", "status", "runtime-status.json"),
            """
            {
              "nextWakeReason": "delayed-provider-retry",
              "delayedRetryUrgency": "alert",
              "nextDelayedRetryAt": "2026-03-23T16:15:00Z"
            }
            """);
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));

        var commands = new List<string>();
        var github = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return string.Empty;
        });
        var loop = new SelfBuildLoop(root, githubIssueService: github);

        var result = loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);

        Assert.False(result.Attempted);
        Assert.False(result.Updated);
        Assert.Contains("Deferred GitHub heartbeat", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(commands, command => command.Contains("issue comment 22", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("dragon-backend-heartbeat", StringComparison.Ordinal));
        Assert.NotNull(loop.ReadStatus().LatestGithubSync);
        Assert.Contains("Deferred GitHub heartbeat", loop.ReadStatus().LatestGithubSync!.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplayPendingGithubSyncs_LeavesPendingSyncQueuedWhenHeartbeatIsDeferred()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, ".dragon", "status"));
        File.WriteAllText(
            Path.Combine(root, ".dragon", "status", "runtime-status.json"),
            """
            {
              "nextWakeReason": "delayed-provider-retry",
              "delayedRetryUrgency": "alert",
              "nextDelayedRetryAt": "2026-03-23T16:15:00Z"
            }
            """);
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));

        var github = new GithubIssueService((_, _) => string.Empty);
        var loop = new SelfBuildLoop(root, githubIssueService: github);
        loop.RecordPendingGithubSyncForTests(22, "GitHub sync failed for issue #22.");

        var replayResults = loop.ReplayPendingGithubSyncs("tmassey1979", "IdeaEngine");
        var status = loop.ReadStatus();

        Assert.Single(replayResults);
        Assert.False(replayResults[0].Attempted);
        Assert.False(replayResults[0].Updated);
        Assert.Contains("Deferred GitHub replay", replayResults[0].Summary, StringComparison.Ordinal);
        Assert.Equal(1, status.PendingGithubSyncCount);
        Assert.Null(status.LatestGithubSync);
        Assert.NotNull(status.LatestGithubReplay);
        Assert.Equal(0, status.LatestGithubReplay!.AttemptedCount);
        Assert.Equal(0, status.LatestGithubReplay.UpdatedCount);
        Assert.Equal(0, status.LatestGithubReplay.FailedCount);
        Assert.Contains("Deferred replay for 1 pending GitHub update", status.LatestGithubReplay.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncValidatedWorkflow_DoesNotRewriteLatestGithubSyncForRepeatedDeferredHeartbeat()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, ".dragon", "status"));
        File.WriteAllText(
            Path.Combine(root, ".dragon", "status", "runtime-status.json"),
            """
            {
              "nextWakeReason": "delayed-provider-retry",
              "delayedRetryUrgency": "alert",
              "nextDelayedRetryAt": "2026-03-23T16:15:00Z"
            }
            """);
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));

        var github = new GithubIssueService((_, _) => string.Empty);
        var loop = new SelfBuildLoop(root, githubIssueService: github);

        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);
        var firstRecordedAt = loop.ReadStatus().LatestGithubSync!.RecordedAt;

        Thread.Sleep(20);
        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);
        var secondRecordedAt = loop.ReadStatus().LatestGithubSync!.RecordedAt;

        Assert.Equal(firstRecordedAt, secondRecordedAt);
    }

    [Fact]
    public void ReplayPendingGithubSyncs_DoesNotRewriteLatestGithubReplayForRepeatedDeferredReplay()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, ".dragon", "status"));
        File.WriteAllText(
            Path.Combine(root, ".dragon", "status", "runtime-status.json"),
            """
            {
              "nextWakeReason": "delayed-provider-retry",
              "delayedRetryUrgency": "alert",
              "nextDelayedRetryAt": "2026-03-23T16:15:00Z"
            }
            """);
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));

        var github = new GithubIssueService((_, _) => string.Empty);
        var loop = new SelfBuildLoop(root, githubIssueService: github);
        loop.RecordPendingGithubSyncForTests(22, "GitHub sync failed for issue #22.");

        loop.ReplayPendingGithubSyncs("tmassey1979", "IdeaEngine");
        var firstRecordedAt = loop.ReadStatus().LatestGithubReplay!.RecordedAt;

        Thread.Sleep(20);
        loop.ReplayPendingGithubSyncs("tmassey1979", "IdeaEngine");
        var secondRecordedAt = loop.ReadStatus().LatestGithubReplay!.RecordedAt;

        Assert.Equal(firstRecordedAt, secondRecordedAt);
    }

    [Fact]
    public void ReadStatus_TreatsRecentGithubReplayRepairAsActiveHealthyWork()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "review", new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "test", new JobExecutionResult("job-test", "test", "success", "done", DateTimeOffset.UtcNow));

        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "developer",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["changedPaths"] = "docs/ARCHITECTURE.md"
                }),
            new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow),
            []);

        var failOnce = true;
        var github = new GithubIssueService((arguments, _) =>
        {
            if (arguments.Contains("issue comment 22", StringComparison.Ordinal) && failOnce)
            {
                failOnce = false;
                throw new InvalidOperationException("gh command failed: gh: Resource not accessible by personal access token (HTTP 403)");
            }

            return string.Empty;
        });

        var loop = new SelfBuildLoop(root, githubIssueService: github);
        loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 22);
        loop.ReplayPendingGithubSyncs("tmassey1979", "IdeaEngine");

        var status = loop.ReadStatus(workerMode: "watch", workerState: "waiting", pollIntervalSeconds: 30);

        Assert.Equal("healthy", status.Health);
        Assert.Contains("replayed", status.AttentionSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("repairing", status.RecentLoopSignal.Mode);
        Assert.Contains("Replayed 1 pending GitHub update", status.RecentLoopSignal.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteStatus_WritesBackendSnapshotJson()
    {
        var root = CreateTempRoot();
        var outputPath = Path.Combine(root, "ui", "dragon-ui", "sample-status.json");
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            610,
            new SelfBuildJobPayload("[Story] Dashboard Status", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "ui/dragon-ui/sample-status.json",
                ["targetOutcome"] = "refresh dashboard status snapshot",
                ["requestedPriority"] = "high",
                ["requestedBlocking"] = "true",
                ["workType"] = "story"
            }));

        var store = new WorkflowStateStore(root);
        store.Update(610, "Dashboard Status", "documentation", new JobExecutionResult("job-2", "documentation", "success", "synced", DateTimeOffset.UtcNow));

        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "documentation",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                610,
                new SelfBuildJobPayload("[Story] Dashboard Status", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["summaryConflictResolution"] = "Pruned stale summarize_issue work because a stronger same-artifact implementation is already queued.",
                    ["supersededSummaryIssues"] = "608"
                }),
            new JobExecutionResult("job-2", "documentation", "success", "synced", DateTimeOffset.UtcNow),
            []);

        var loop = new SelfBuildLoop(root);
        var status = loop.WriteStatus(outputPath);

        Assert.True(File.Exists(outputPath));
        Assert.Equal(1, status.QueuedJobs);

        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        var rootElement = document.RootElement;
        Assert.Equal("status", rootElement.GetProperty("source").GetString());
        Assert.Equal("status", rootElement.GetProperty("lastCommand").GetString());
        Assert.Equal("status", rootElement.GetProperty("workerMode").GetString());
        Assert.Equal("snapshot", rootElement.GetProperty("workerState").GetString());
        Assert.Equal(JsonValueKind.Null, rootElement.GetProperty("nextPollAt").ValueKind);
        Assert.True(rootElement.TryGetProperty("generatedAt", out _));
        Assert.Equal("healthy", rootElement.GetProperty("health").GetString());
        Assert.Contains("queued job", rootElement.GetProperty("attentionSummary").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, rootElement.GetProperty("rollup").GetProperty("failedIssues").GetInt32());
        Assert.Equal(0, rootElement.GetProperty("rollup").GetProperty("quarantinedIssues").GetInt32());
        Assert.Equal(0, rootElement.GetProperty("rollup").GetProperty("actionableQuarantinedIssues").GetInt32());
        Assert.Equal(0, rootElement.GetProperty("rollup").GetProperty("inactiveQuarantinedIssues").GetInt32());
        Assert.Equal(1, rootElement.GetProperty("rollup").GetProperty("inProgressIssues").GetInt32());
        Assert.Equal(610, rootElement.GetProperty("leadJob").GetProperty("issueNumber").GetInt32());
        Assert.Equal("documentation", rootElement.GetProperty("leadJob").GetProperty("agent").GetString());
        Assert.Equal("implement_issue", rootElement.GetProperty("leadJob").GetProperty("action").GetString());
        Assert.Equal("ui/dragon-ui/sample-status.json", rootElement.GetProperty("leadJob").GetProperty("targetArtifact").GetString());
        Assert.Equal("refresh dashboard status snapshot", rootElement.GetProperty("leadJob").GetProperty("targetOutcome").GetString());
        Assert.Equal("high", rootElement.GetProperty("leadJob").GetProperty("priority").GetString());
        Assert.True(rootElement.GetProperty("leadJob").GetProperty("blocking").GetBoolean());
        Assert.Equal("story", rootElement.GetProperty("leadJob").GetProperty("workType").GetString());
        Assert.Equal(610, rootElement.GetProperty("latestActivity").GetProperty("issueNumber").GetInt32());
        Assert.Equal("documentation", rootElement.GetProperty("latestActivity").GetProperty("currentStage").GetString());
        Assert.Equal("draining", rootElement.GetProperty("recentLoopSignal").GetProperty("mode").GetString());
        Assert.Equal("unknown", rootElement.GetProperty("queueDirection").GetString());
        Assert.Equal(0, rootElement.GetProperty("queueDelta").GetInt32());
        Assert.Equal(JsonValueKind.Null, rootElement.GetProperty("queueComparedAt").ValueKind);
        Assert.Equal(0, rootElement.GetProperty("rollupDelta").GetProperty("failedIssues").GetInt32());
        Assert.Equal(0, rootElement.GetProperty("rollupDelta").GetProperty("inProgressIssues").GetInt32());
        Assert.Equal(1, rootElement.GetProperty("queuedJobs").GetInt32());
        Assert.Equal(JsonValueKind.Null, rootElement.GetProperty("pendingGithubSyncSummary").ValueKind);
        Assert.Equal(JsonValueKind.Null, rootElement.GetProperty("latestGithubReplay").ValueKind);
        Assert.Equal(0, rootElement.GetProperty("pendingGithubSync").GetArrayLength());
        Assert.Equal("implementation", rootElement.GetProperty("interventionTarget").GetProperty("kind").GetString());
        Assert.Equal(610, rootElement.GetProperty("interventionTarget").GetProperty("issueNumber").GetInt32());
        Assert.Equal("ui/dragon-ui/sample-status.json", rootElement.GetProperty("interventionTarget").GetProperty("targetArtifact").GetString());
        Assert.Equal("refresh dashboard status snapshot", rootElement.GetProperty("interventionTarget").GetProperty("targetOutcome").GetString());
        Assert.Equal("fresh", rootElement.GetProperty("interventionTarget").GetProperty("escalation").GetString());
        Assert.Equal(JsonValueKind.Null, rootElement.GetProperty("interventionEscalationNote").ValueKind);
        Assert.Equal("Advance issue #610: refresh dashboard status snapshot.", rootElement.GetProperty("workerActivity").GetString());

        var issueElement = Assert.Single(rootElement.GetProperty("issues").EnumerateArray());
        Assert.Equal(610, issueElement.GetProperty("issueNumber").GetInt32());
        Assert.Equal("documentation", issueElement.GetProperty("currentStage").GetString());
        Assert.Contains("Superseded summary issues: 608", issueElement.GetProperty("latestExecutionNotes").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatusHttpServer_ServesLiveStatusSnapshot()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            710,
            new SelfBuildJobPayload("[Story] Live Status", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "ui/dragon-ui/sample-status.json",
                ["targetOutcome"] = "serve a live dashboard snapshot",
                ["requestedPriority"] = "high",
                ["requestedBlocking"] = "true",
                ["workType"] = "story"
            }));

        var loop = new SelfBuildLoop(root);
        var server = new StatusHttpServer(loop);
        var prefix = CreateLocalHttpPrefix();
        var serveTask = server.ServeOnceAsync(prefix);

        using var client = new HttpClient();
        var snapshot = await client.GetFromJsonAsync<StatusSnapshot>($"{prefix}status");
        await serveTask;

        Assert.NotNull(snapshot);
        Assert.Equal("status-http", snapshot!.Source);
        Assert.Equal("serve-status", snapshot.LastCommand);
        Assert.Equal(1, snapshot.QueuedJobs);
        Assert.NotNull(snapshot.LeadJob);
        Assert.Equal(710, snapshot.LeadJob!.IssueNumber);
        Assert.Equal("ui/dragon-ui/sample-status.json", snapshot.LeadJob.TargetArtifact);
    }

    [Fact]
    public async Task StatusHttpServer_PrefersRuntimeSnapshotFile_WhenProvided()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var snapshotPath = Path.Combine(root, ".dragon", "status", "runtime-status.json");
        loop.WriteStatus(
            snapshotPath,
            "run-watch",
            "watch",
            "waiting",
            null,
            DateTimeOffset.UtcNow.AddSeconds(30),
            30,
            1,
            2,
            1,
            4,
            SelfBuildLoop.BuildLatestPassSummary(1, new RunUntilIdleResult([], true, false)),
            1,
            4);

        var server = new StatusHttpServer(loop, snapshotPath);
        var prefix = CreateLocalHttpPrefix();
        var serveTask = server.ServeOnceAsync(prefix);

        using var client = new HttpClient();
        var snapshot = await client.GetFromJsonAsync<StatusSnapshot>($"{prefix}status");
        await serveTask;

        Assert.NotNull(snapshot);
        Assert.Equal("status-http", snapshot!.Source);
        Assert.Equal("run-watch", snapshot.LastCommand);
        Assert.Equal("watch", snapshot.WorkerMode);
        Assert.Equal("waiting", snapshot.WorkerState);
        Assert.Equal(30, snapshot.PollIntervalSeconds);
    }

    [Fact]
    public async Task StatusHttpServer_FallsBackWhenRuntimeSnapshotFileIsInvalid()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            711,
            new SelfBuildJobPayload("[Story] Runtime Fallback", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "ui/dragon-ui/sample-status.json",
                ["targetOutcome"] = "serve a fallback dashboard snapshot",
                ["requestedPriority"] = "high",
                ["requestedBlocking"] = "true",
                ["workType"] = "story"
            }));

        var snapshotPath = Path.Combine(root, ".dragon", "status", "runtime-status.json");
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        File.WriteAllText(snapshotPath, "{");

        var loop = new SelfBuildLoop(root);
        var server = new StatusHttpServer(loop, snapshotPath);
        var prefix = CreateLocalHttpPrefix();
        var serveTask = server.ServeOnceAsync(prefix);

        using var client = new HttpClient();
        var snapshot = await client.GetFromJsonAsync<StatusSnapshot>($"{prefix}status");
        await serveTask;

        Assert.NotNull(snapshot);
        Assert.Equal("status-http", snapshot!.Source);
        Assert.Equal("serve-status", snapshot.LastCommand);
        Assert.Equal(1, snapshot.QueuedJobs);
        Assert.NotNull(snapshot.LeadJob);
        Assert.Equal(711, snapshot.LeadJob!.IssueNumber);
    }

    [Fact]
    public async Task StatusHttpServer_IncludesCorsHeadersOnStatusResponses()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var server = new StatusHttpServer(loop);
        var prefix = CreateLocalHttpPrefix();
        var serveTask = server.ServeOnceAsync(prefix);

        using var client = new HttpClient();
        using var response = await client.GetAsync($"{prefix}status");
        await serveTask;

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Contains("*", values);
    }

    [Fact]
    public async Task StatusHttpServer_ReturnsNotFoundForUnknownRoute()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var server = new StatusHttpServer(loop);
        var prefix = CreateLocalHttpPrefix();
        var serveTask = server.ServeOnceAsync(prefix);

        using var client = new HttpClient();
        var response = await client.GetAsync($"{prefix}missing");
        await serveTask;

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void SeedNext_PrioritizesRecoveryIssuesOverOrdinaryStories()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var issues = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"]),
            new GithubIssue(500, "[Recovery] Issue #22: Core System Principles", "OPEN", ["story", "recovery", "backlog"], SourceIssueNumber: 22)
        };

        var job = loop.SeedNext(issues);

        Assert.Equal(500, job.Issue);
        Assert.Equal("recover_issue", job.Action);
        Assert.Equal("recovery", job.Metadata["workType"]);
        Assert.Equal("22", job.Metadata["sourceIssueNumber"]);
    }

    [Fact]
    public void SeedNext_PrefersLatestUnresolvedRecoveryIssueForSameParent()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var issues = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"]),
            new GithubIssue(500, "[Recovery] Issue #22: Core System Principles", "OPEN", ["story", "recovery", "backlog"], SourceIssueNumber: 22),
            new GithubIssue(501, "[Recovery] Issue #22: Core System Principles Follow-up", "OPEN", ["story", "recovery", "backlog"], SourceIssueNumber: 22)
        };

        var job = loop.SeedNext(issues);

        Assert.Equal(501, job.Issue);
        Assert.Equal("recover_issue", job.Action);
        Assert.Equal("22", job.Metadata["sourceIssueNumber"]);
    }

    [Fact]
    public void SeedNext_DoesNotPrioritizeRecoveryIssueAfterParentRecoveryHoldIsReleased()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-parent", "developer", "failed", "blocked", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(22, "in_progress", "Recovery child completed; parent returned to active flow.");

        var loop = new SelfBuildLoop(root);
        var issues = new[]
        {
            new GithubIssue(23, "[Story] Dragon Idea Engine Master Codex: System Architecture", "OPEN", ["story"]),
            new GithubIssue(500, "[Recovery] Issue #22: Core System Principles", "OPEN", ["story", "recovery", "backlog"], SourceIssueNumber: 22)
        };

        var job = loop.SeedNext(issues);

        Assert.Equal(23, job.Issue);
        Assert.Equal("implement_issue", job.Action);
    }

    [Fact]
    public void CycleOnce_RemovesSupersededRecoveryJobsBeforeConsumingQueue()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(SelfBuildJobFactory.Create(
            new GithubIssue(500, "[Recovery] Issue #22: Core System Principles", "OPEN", ["story", "recovery", "backlog"], SourceIssueNumber: 22),
            "developer",
            "IdeaEngine",
            "DragonIdeaEngine"
        ));
        queue.Enqueue(SelfBuildJobFactory.Create(
            new GithubIssue(501, "[Recovery] Issue #22: Core System Principles Follow-up", "OPEN", ["story", "recovery", "backlog"], SourceIssueNumber: 22),
            "developer",
            "IdeaEngine",
            "DragonIdeaEngine"
        ));

        var issues = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"]),
            new GithubIssue(500, "[Recovery] Issue #22: Core System Principles", "OPEN", ["story", "recovery", "backlog"], SourceIssueNumber: 22),
            new GithubIssue(501, "[Recovery] Issue #22: Core System Principles Follow-up", "OPEN", ["story", "recovery", "backlog"], SourceIssueNumber: 22)
        };

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var loop = new SelfBuildLoop(root, jobExecutor: executor);

        var result = loop.CycleOnce(issues);

        Assert.Equal("consume", result.Mode);
        Assert.NotNull(result.Job);
        Assert.Equal(501, result.Job!.Issue);
        Assert.DoesNotContain(loop.ReadQueue(), job => job.Issue == 500);
    }

    [Fact]
    public void SeedNext_SkipsParentIssueWhenRecoveryChildIsActive()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-parent", "developer", "failed", "blocked", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(22, "quarantined", "Parent is quarantined.");

        var recoveryJob = new SelfBuildJob(
            "developer",
            "recover_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            500,
            new SelfBuildJobPayload("[Recovery] Issue #22: Core", ["story", "recovery"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceIssueNumber"] = "22",
                ["workType"] = "recovery"
            }
        );
        store.Update(recoveryJob, new JobExecutionResult("job-recovery", "developer", "success", "started", DateTimeOffset.UtcNow));

        var loop = new SelfBuildLoop(root);
        var issues = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"]),
            new GithubIssue(23, "[Story] Dragon Idea Engine Master Codex: System Architecture", "OPEN", ["story"])
        };

        var job = loop.SeedNext(issues);

        Assert.Equal(23, job.Issue);
    }

    [Fact]
    public void CycleOnce_RequeuesParentAfterRecoveryHoldIsReleased()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-parent", "developer", "failed", "blocked", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(22, "quarantined", "Parent is quarantined.");

        var recoveryJob = new SelfBuildJob(
            "developer",
            "recover_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            500,
            new SelfBuildJobPayload("[Recovery] Issue #22: Core", ["story", "recovery"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceIssueNumber"] = "22",
                ["workType"] = "recovery"
            }
        );

        store.Update(recoveryJob, new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(recoveryJob, new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(recoveryJob, new JobExecutionResult("job-test", "test", "success", "done", DateTimeOffset.UtcNow));

        var loop = new SelfBuildLoop(root);
        var issues = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"]),
            new GithubIssue(23, "[Story] Dragon Idea Engine Master Codex: System Architecture", "OPEN", ["story"])
        };

        var result = loop.CycleOnce(issues);

        Assert.Equal("resume", result.Mode);
        Assert.NotNull(result.Job);
        Assert.Equal(22, result.Job!.Issue);
        Assert.Equal("implement_issue", result.Job.Action);
        Assert.Equal("Recovery child completed; parent requeued for active flow.", result.Workflow!.Note);
        Assert.Single(loop.ReadQueue());
    }

    [Fact]
    public void CycleOnce_SeedsThenExecutesDeveloperFlow()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var stories = new[]
        {
            new GithubIssue(
                22,
                "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                "OPEN",
                ["story"],
                "",
                "Core System Principles",
                "codex/sections/01-dragon-idea-engine-master-codex.md"
            )
        };

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var seed = loop.CycleOnce(stories);
        var consumeDeveloper = loop.CycleOnce(stories);
        var consumeReview = loop.CycleOnce(stories);
        var consumeTest = loop.CycleOnce(stories);

        Assert.Equal("seed", seed.Mode);
        Assert.Equal("consume", consumeDeveloper.Mode);
        Assert.Equal("success", consumeDeveloper.Execution!.Status);
        Assert.Equal(2, consumeDeveloper.FollowUps.Count);
        Assert.Equal("consume", consumeReview.Mode);
        Assert.Equal("consume", consumeTest.Mode);

        var statePath = Path.Combine(root, ".dragon", "state", "issues.json");
        var recordPath = Path.Combine(root, ".dragon", "runs", "issue-22.json");
        Assert.True(File.Exists(statePath));
        Assert.True(File.Exists(recordPath));
        var state = File.ReadAllText(statePath);
        Assert.Contains("validated", state, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Core System Principles", File.ReadAllText(Path.Combine(root, "docs", "ARCHITECTURE.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void CycleOnce_PublishesFollowUpsWithExecutionChangedPaths()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var stories = new[]
        {
            new GithubIssue(
                22,
                "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                "OPEN",
                ["story"],
                "",
                "Core System Principles",
                "codex/sections/01-dragon-idea-engine-master-codex.md"
            )
        };

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var loop = new SelfBuildLoop(root, jobExecutor: executor);

        loop.CycleOnce(stories);
        var result = loop.CycleOnce(stories);

        Assert.Equal("consume", result.Mode);
        Assert.Equal(2, result.FollowUps.Count);
        Assert.All(result.FollowUps, followUp => Assert.Equal("docs/ARCHITECTURE.md", followUp.Metadata["changedPaths"]));
        Assert.Contains(result.ExecutionRecord!.ChangedPaths, path => path == "docs/ARCHITECTURE.md");
    }

    [Fact]
    public void RunUntilIdle_DrainsLocalSelfBuildFlowToCompletion()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var stories = new[]
        {
            new GithubIssue(
                22,
                "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                "OPEN",
                ["story"],
                "",
                "Core System Principles",
                "codex/sections/01-dragon-idea-engine-master-codex.md"
            )
        };

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var loop = new SelfBuildLoop(root, jobExecutor: executor);

        var result = loop.RunUntilIdle(stories, maxCycles: 10);

        Assert.True(result.ReachedIdle);
        Assert.False(result.ReachedMaxCycles);
        Assert.Equal(4, result.Cycles.Count);
        Assert.Equal(["seed", "consume", "consume", "consume"], result.Cycles.Select(cycle => cycle.Mode));

        var statePath = Path.Combine(root, ".dragon", "state", "issues.json");
        var state = File.ReadAllText(statePath);
        Assert.Contains("validated", state, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(loop.ReadQueue());
    }

    [Fact]
    public void RunUntilIdle_CanExportFreshStatusSnapshotAfterCompletion()
    {
        var root = CreateTempRoot();
        var outputPath = Path.Combine(root, "ui", "dragon-ui", "sample-status.json");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var stories = new[]
        {
            new GithubIssue(
                22,
                "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                "OPEN",
                ["story"],
                "",
                "Core System Principles",
                "codex/sections/01-dragon-idea-engine-master-codex.md"
            )
        };

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var loop = new SelfBuildLoop(root, jobExecutor: executor);

        var result = loop.RunUntilIdle(stories, maxCycles: 10);
        var snapshot = loop.WriteStatus(outputPath);

        Assert.True(result.ReachedIdle);
        Assert.True(File.Exists(outputPath));
        Assert.Empty(loop.ReadQueue());
        Assert.Equal(0, snapshot.QueuedJobs);

        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        var rootElement = document.RootElement;
        Assert.Equal("status", rootElement.GetProperty("source").GetString());
        Assert.Equal("status", rootElement.GetProperty("lastCommand").GetString());
        Assert.True(rootElement.TryGetProperty("generatedAt", out _));
        Assert.Equal("idle", rootElement.GetProperty("health").GetString());
        Assert.Contains("No queued work", rootElement.GetProperty("attentionSummary").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, rootElement.GetProperty("rollup").GetProperty("failedIssues").GetInt32());
        Assert.Equal(0, rootElement.GetProperty("rollup").GetProperty("quarantinedIssues").GetInt32());
        Assert.Equal(0, rootElement.GetProperty("rollup").GetProperty("inProgressIssues").GetInt32());
        Assert.Equal(1, rootElement.GetProperty("rollup").GetProperty("validatedIssues").GetInt32());
        Assert.Equal(22, rootElement.GetProperty("latestActivity").GetProperty("issueNumber").GetInt32());
        Assert.Equal("test", rootElement.GetProperty("latestActivity").GetProperty("currentStage").GetString());
        Assert.Equal("idle", rootElement.GetProperty("recentLoopSignal").GetProperty("mode").GetString());
        Assert.Equal("unknown", rootElement.GetProperty("queueDirection").GetString());
        Assert.Equal(0, rootElement.GetProperty("queueDelta").GetInt32());
        Assert.Equal(JsonValueKind.Null, rootElement.GetProperty("queueComparedAt").ValueKind);
        Assert.Equal(0, rootElement.GetProperty("rollupDelta").GetProperty("validatedIssues").GetInt32());
        Assert.Equal(0, rootElement.GetProperty("queuedJobs").GetInt32());

        var issueElement = Assert.Single(rootElement.GetProperty("issues").EnumerateArray());
        Assert.Equal(22, issueElement.GetProperty("issueNumber").GetInt32());
        Assert.Equal("validated", issueElement.GetProperty("overallStatus").GetString());
    }

    [Fact]
    public void RunPolling_ReachesIdleThresholdAfterDrainingWork()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var stories = new[]
        {
            new GithubIssue(
                22,
                "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                "OPEN",
                ["story"],
                "",
                "Core System Principles",
                "codex/sections/01-dragon-idea-engine-master-codex.md"
            )
        };

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var loop = new SelfBuildLoop(root, jobExecutor: executor);

        var result = loop.RunPolling(stories, maxPasses: 3, idlePassesBeforeStop: 2, maxCyclesPerPass: 10);

        Assert.True(result.ReachedIdleThreshold);
        Assert.False(result.ReachedMaxPasses);
        Assert.Equal(2, result.ConsecutiveIdlePasses);
        Assert.Equal(2, result.Passes.Count);
        Assert.NotEmpty(result.Passes[0].Cycles);
        Assert.Empty(result.Passes[1].Cycles);
    }

    [Fact]
    public void RunPolling_StopsAtMaxPassesWhenIdleThresholdIsNotReached()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);

        var result = loop.RunPolling([], maxPasses: 2, idlePassesBeforeStop: 3, maxCyclesPerPass: 10);

        Assert.False(result.ReachedIdleThreshold);
        Assert.True(result.ReachedMaxPasses);
        Assert.Equal(2, result.ConsecutiveIdlePasses);
        Assert.Equal(2, result.Passes.Count);
        Assert.All(result.Passes, pass =>
        {
            Assert.True(pass.ReachedIdle);
            Assert.Empty(pass.Cycles);
        });
    }

    [Fact]
    public void RunWatching_DelaysBetweenPassesUntilIdleThresholdIsReached()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var delays = new List<TimeSpan>();

        var result = loop.RunWatching(
            [],
            TimeSpan.FromSeconds(15),
            maxPasses: 3,
            idlePassesBeforeStop: 2,
            maxCyclesPerPass: 10,
            delayAction: delays.Add);

        Assert.True(result.ReachedIdleThreshold);
        Assert.False(result.ReachedMaxPasses);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(15), delays[0]);
    }

    [Fact]
    public void RunWatching_DoesNotDelayAfterFinalPass()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var delays = new List<TimeSpan>();

        var result = loop.RunWatching(
            [],
            TimeSpan.FromSeconds(15),
            maxPasses: 2,
            idlePassesBeforeStop: 3,
            maxCyclesPerPass: 10,
            delayAction: delays.Add);

        Assert.False(result.ReachedIdleThreshold);
        Assert.True(result.ReachedMaxPasses);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(15), delays[0]);
    }

    [Fact]
    public void RunWatching_WaitsUntilDelayedRetryWindowWhenOnlyDelayedWorkRemains()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var queue = new QueueStore(root, nowProvider: () => now);
        queue.Enqueue(new SelfBuildJob(
            "architect",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("[Story] Delayed retry", ["story"], "Architect Agent", "codex/sections/01-dragon-idea-engine-master-codex.md", null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retryNotBeforeUtc"] = now.AddMinutes(5).ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            }));
        var loop = new SelfBuildLoop(root, nowProvider: () => now);
        var delays = new List<TimeSpan>();

        var result = loop.RunWatching(
            [],
            TimeSpan.FromSeconds(15),
            maxPasses: 2,
            idlePassesBeforeStop: 1,
            maxCyclesPerPass: 10,
            delayAction: delays.Add);

        Assert.False(result.ReachedIdleThreshold);
        Assert.True(result.ReachedMaxPasses);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromMinutes(5), delays[0]);
    }

    [Fact]
    public void RunWatchingFromGithub_CapsDelayedRetryWaitToPollInterval()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        Directory.CreateDirectory(Path.Combine(root, "planning"));
        File.WriteAllText(
            Path.Combine(root, "planning", "backlog.json"),
            """
            {
              "stories": []
            }
            """);
        var queue = new QueueStore(root, nowProvider: () => now);
        queue.Enqueue(new SelfBuildJob(
            "architect",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("[Story] Delayed retry", ["story"], "Architect Agent", "codex/sections/01-dragon-idea-engine-master-codex.md", null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retryNotBeforeUtc"] = now.AddMinutes(5).ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            }));
        var github = new GithubIssueService((arguments, _) =>
        {
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            return string.Empty;
        });
        var loop = new SelfBuildLoop(root, githubIssueService: github, nowProvider: () => now);
        var delays = new List<TimeSpan>();

        var result = loop.RunWatchingFromGithub(
            "tmassey1979",
            "IdeaEngine",
            TimeSpan.FromSeconds(15),
            maxPasses: 2,
            idlePassesBeforeStop: 1,
            maxCyclesPerPass: 10,
            delayAction: delays.Add);

        Assert.False(result.ReachedIdleThreshold);
        Assert.True(result.ReachedMaxPasses);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(15), delays[0]);
    }

    [Fact]
    public void GetWatchDelay_ReturnsDelayedRetryWindowForLocalWatch()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var queue = new QueueStore(root, nowProvider: () => now);
        queue.Enqueue(new SelfBuildJob(
            "architect",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("[Story] Delayed retry", ["story"], "Architect Agent", "codex/sections/01-dragon-idea-engine-master-codex.md", null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retryNotBeforeUtc"] = now.AddMinutes(5).ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            }));
        var loop = new SelfBuildLoop(root, nowProvider: () => now);

        var delay = loop.GetWatchDelay(TimeSpan.FromSeconds(15));

        Assert.Equal(TimeSpan.FromMinutes(5), delay);
    }

    [Fact]
    public void GetWatchDelay_CapsDelayedRetryWindowForGithubWatch()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var queue = new QueueStore(root, nowProvider: () => now);
        queue.Enqueue(new SelfBuildJob(
            "architect",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("[Story] Delayed retry", ["story"], "Architect Agent", "codex/sections/01-dragon-idea-engine-master-codex.md", null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retryNotBeforeUtc"] = now.AddMinutes(5).ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            }));
        var loop = new SelfBuildLoop(root, nowProvider: () => now);

        var delay = loop.GetWatchDelay(TimeSpan.FromSeconds(15), capToPollInterval: true);

        Assert.Equal(TimeSpan.FromSeconds(15), delay);
    }

    [Fact]
    public void BuildLatestPassSummary_CapturesPassOutcomeCounts()
    {
        var escalationFollowUp = new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("Issue 22", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workType"] = "operator-escalation"
            });
        var pass = new RunUntilIdleResult(
            [
                new CycleResult("seed", null, null, [escalationFollowUp]),
                new CycleResult("consume", escalationFollowUp, null, []),
                new CycleResult("consume", null, null, [])
            ],
            true,
            false);

        var summary = SelfBuildLoop.BuildLatestPassSummary(3, pass);

        Assert.Equal(3, summary.PassNumber);
        Assert.Equal(3, summary.CycleCount);
        Assert.Equal(1, summary.SeededCycles);
        Assert.Equal(2, summary.ConsumedCycles);
        Assert.True(summary.ReachedIdle);
        Assert.False(summary.ReachedMaxCycles);
        Assert.Equal(0, summary.GithubReplayAttemptedCount);
        Assert.Null(summary.GithubReplaySummary);
        Assert.Equal(1, summary.OperatorEscalationQueuedCount);
        Assert.Equal(1, summary.OperatorEscalationConsumedCount);
    }

    [Fact]
    public void BuildLatestPassSummary_CapturesGithubReplayCounts()
    {
        var pass = new RunUntilIdleResult(
            [
                new CycleResult("consume", null, null, [])
            ],
            false,
            false);

        var replay = new LatestGithubReplaySnapshot(
            4,
            3,
            1,
            "Replayed 4 pending GitHub updates: 3 updated, 1 still failing.",
            DateTimeOffset.UtcNow);

        var summary = SelfBuildLoop.BuildLatestPassSummary(2, pass, replay);

        Assert.Equal(4, summary.GithubReplayAttemptedCount);
        Assert.Equal(3, summary.GithubReplayUpdatedCount);
        Assert.Equal(1, summary.GithubReplayFailedCount);
        Assert.Equal("Replayed 4 pending GitHub updates: 3 updated, 1 still failing.", summary.GithubReplaySummary);
        Assert.Equal(0, summary.OperatorEscalationQueuedCount);
        Assert.Equal(0, summary.OperatorEscalationConsumedCount);
    }

    [Fact]
    public void RunWatching_ReportsEachCompletedPass()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var completedPasses = new List<int>();

        var result = loop.RunWatching(
            [],
            TimeSpan.FromSeconds(15),
            maxPasses: 3,
            idlePassesBeforeStop: 2,
            maxCyclesPerPass: 10,
            delayAction: _ => { },
            passCompleted: (passNumber, _) => completedPasses.Add(passNumber));

        Assert.True(result.ReachedIdleThreshold);
        Assert.Equal([1, 2], completedPasses);
    }

    [Fact]
    public void RunPolling_ReportsEachCompletedPassUntilMaxPasses()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var completedPasses = new List<int>();

        var result = loop.RunPolling(
            [],
            maxPasses: 2,
            idlePassesBeforeStop: 3,
            maxCyclesPerPass: 10,
            passCompleted: (passNumber, _) => completedPasses.Add(passNumber));

        Assert.True(result.ReachedMaxPasses);
        Assert.Equal([1, 2], completedPasses);
    }

    [Fact]
    public void StatusSnapshotTrend_ComparesQueuedJobsAgainstPreviousSnapshot()
    {
        var previous = new StatusSnapshot(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            "status",
            "run-watch",
            "status",
            "snapshot",
            null,
            null,
            null,
            0,
            0,
            null,
            null,
            null,
            null,
            "healthy",
            "previous",
            new StatusRollup(0, 0, 0, 0, 1, 0),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("draining", "previous"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            3,
            []);
        var current = new StatusSnapshot(
            DateTimeOffset.UtcNow,
            "status",
            "run-watch",
            "watch",
            "waiting",
            null,
            DateTimeOffset.UtcNow.AddMinutes(1),
            30,
            1,
            2,
            1,
            4,
            2,
            6,
            "healthy",
            "current",
            new StatusRollup(1, 0, 0, 0, 0, 1),
            new LeadJobSnapshot(500, "Provider Notes", "documentation", "implement_issue", "docs/generated/provider-notes.md", "refresh provider notes summary", "high", true, "story"),
            null,
            null,
            new RecentLoopSignalSnapshot("draining", "current"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            1,
            []);

        var annotated = StatusSnapshotTrend.Apply(current, previous);

        Assert.Equal("down", annotated.QueueDirection);
        Assert.Equal(-2, annotated.QueueDelta);
        Assert.Equal(previous.GeneratedAt, annotated.QueueComparedAt);
        Assert.Equal(1, annotated.RollupDelta.FailedIssues);
        Assert.Equal(-1, annotated.RollupDelta.InProgressIssues);
        Assert.Equal(1, annotated.RollupDelta.ValidatedIssues);
        Assert.Equal("run-watch", annotated.LastCommand);
        Assert.Equal("watch", annotated.WorkerMode);
        Assert.Equal("waiting", annotated.WorkerState);
        Assert.Null(annotated.WorkerCompletionReason);
        Assert.Equal(30, annotated.PollIntervalSeconds);
        Assert.Equal(1, annotated.IdleStreak);
        Assert.Equal(2, annotated.IdleTarget);
        Assert.Equal(1, annotated.IdlePassesRemaining);
        Assert.Equal(4, annotated.PassBudgetRemaining);
        Assert.Equal(2, annotated.CurrentPassNumber);
        Assert.Equal(6, annotated.MaxPasses);
        Assert.NotNull(annotated.LeadJob);
        Assert.Equal(500, annotated.LeadJob!.IssueNumber);
        Assert.Equal("docs/generated/provider-notes.md", annotated.LeadJob.TargetArtifact);
        Assert.Equal("refresh provider notes summary", annotated.LeadJob.TargetOutcome);
        Assert.Equal("high", annotated.LeadJob.Priority);
        Assert.True(annotated.LeadJob.Blocking);
        Assert.Equal("story", annotated.LeadJob.WorkType);
        Assert.Equal(0, annotated.InterventionEscalationStreak);
    }

    [Fact]
    public void StatusSnapshotTrend_IncrementsCriticalInterventionEscalationStreakForSameTarget()
    {
        var previous = new StatusSnapshot(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            "status",
            "run-watch",
            "watch",
            "waiting",
            null,
            null,
            30,
            0,
            2,
            2,
            8,
            1,
            8,
            "healthy",
            "previous",
            new StatusRollup(0, 0, 0, 0, 0, 1),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("repairing", "previous"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            0,
            [],
            null,
            null,
            null,
            0,
            [],
            null,
            null,
            new InterventionTargetSnapshot(
                "github-replay-drift",
                "Replay queued GitHub update for issue #147.",
                147,
                null,
                147,
                null,
                null,
                DateTimeOffset.UtcNow.AddHours(-2),
                "2h 0m old",
                "critical"),
            "Escalation: global intervention target is critical. Replay queued GitHub update for issue #147.",
            2);

        var current = new StatusSnapshot(
            DateTimeOffset.UtcNow,
            "status",
            "run-watch",
            "watch",
            "waiting",
            null,
            null,
            30,
            0,
            2,
            2,
            7,
            2,
            8,
            "healthy",
            "current",
            new StatusRollup(0, 0, 0, 0, 0, 1),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("repairing", "current"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            0,
            [],
            null,
            null,
            null,
            0,
            [],
            null,
            null,
            new InterventionTargetSnapshot(
                "github-replay-drift",
                "Replay queued GitHub update for issue #147.",
                147,
                null,
                147,
                null,
                null,
                DateTimeOffset.UtcNow.AddHours(-2),
                "2h 0m old",
                "critical"),
            "Escalation: global intervention target is critical. Replay queued GitHub update for issue #147.");

        var annotated = StatusSnapshotTrend.Apply(current, previous);

        Assert.Equal(3, annotated.InterventionEscalationStreak);
        Assert.Contains("Persisting across 3 consecutive status snapshots", annotated.InterventionEscalationNote, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusSnapshotTrend_IncrementsAcknowledgedInterventionTargetStreakForSameTarget()
    {
        var previous = new StatusSnapshot(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            "status",
            "run-watch",
            "watch",
            "waiting",
            null,
            null,
            30,
            0,
            2,
            2,
            8,
            1,
            8,
            "healthy",
            "previous",
            new StatusRollup(0, 0, 0, 0, 0, 1),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("monitoring", "previous"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            0,
            [],
            null,
            null,
            null,
            0,
            [],
            null,
            null,
            new InterventionTargetSnapshot(
                "operator-escalation",
                "Escalate issue #22: Summarize the persistent critical intervention target and the next operator action.",
                22,
                null,
                null,
                "backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs",
                "Summarize the persistent critical intervention target and the next operator action.",
                DateTimeOffset.UtcNow.AddHours(-2),
                "2h 0m old",
                "critical",
                true,
                2),
            "Escalation: global intervention target is critical. Summarize the persistent critical intervention target and the next operator action.",
            3);

        var current = new StatusSnapshot(
            DateTimeOffset.UtcNow,
            "status",
            "run-watch",
            "watch",
            "waiting",
            null,
            null,
            30,
            0,
            2,
            2,
            7,
            2,
            8,
            "healthy",
            "current",
            new StatusRollup(0, 0, 0, 0, 0, 1),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("monitoring", "current"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            0,
            [],
            null,
            null,
            null,
            0,
            [],
            null,
            null,
            new InterventionTargetSnapshot(
                "operator-escalation",
                "Escalate issue #22: Summarize the persistent critical intervention target and the next operator action.",
                22,
                null,
                null,
                "backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs",
                "Summarize the persistent critical intervention target and the next operator action.",
                DateTimeOffset.UtcNow.AddHours(-2),
                "2h 0m old",
                "critical",
                true),
            "Escalation: global intervention target is critical. Summarize the persistent critical intervention target and the next operator action.");

        var annotated = StatusSnapshotTrend.Apply(current, previous);

        Assert.NotNull(annotated.InterventionTarget);
        Assert.Equal(3, annotated.InterventionTarget!.AcknowledgedStreak);
        Assert.Contains("remains critical after acknowledgment for 3 consecutive status snapshots", annotated.InterventionEscalationNote, StringComparison.Ordinal);
    }

    [Fact]
    public void EnqueuePersistentInterventionEscalationFollowUp_QueuesSingleOperatorSummaryForCriticalTarget()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var snapshot = new StatusSnapshot(
            DateTimeOffset.UtcNow,
            "status",
            "github-run-watch",
            "github-run-watch",
            "waiting",
            null,
            null,
            30,
            0,
            2,
            2,
            7,
            3,
            10,
            "healthy",
            "repairing drift",
            new StatusRollup(0, 0, 0, 0, 0, 1),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("repairing", "repairing drift"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            0,
            [],
            null,
            null,
            null,
            0,
            [],
            null,
            null,
            new InterventionTargetSnapshot(
                "github-replay-drift",
                "Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.",
                22,
                500,
                500,
                "backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs",
                "Summarize the persistent critical intervention target and the next operator action.",
                DateTimeOffset.UtcNow.AddHours(-2),
                "2h 0m old",
                "critical"),
            "Escalation: global intervention target is critical. Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.",
            3);

        var job = loop.EnqueuePersistentInterventionEscalationFollowUp(snapshot);

        Assert.NotNull(job);
        Assert.Equal(22, job!.Issue);
        Assert.Equal("summarize_issue", job.Action);
        Assert.Equal("feedback", job.Agent);
        Assert.Equal("true", job.Metadata["interventionEscalation"]);
        Assert.Equal("github-replay-drift|22|500|500|backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs|Summarize the persistent critical intervention target and the next operator action.", job.Metadata["interventionSignature"]);
        Assert.Equal("3", job.Metadata["interventionEscalationStreak"]);
        Assert.Equal("operator-escalation", job.Metadata["workType"]);
        Assert.Single(loop.ReadQueue());
    }

    [Fact]
    public void EnqueuePersistentInterventionEscalationFollowUp_DoesNotDuplicateExistingEscalationSummary()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var snapshot = new StatusSnapshot(
            DateTimeOffset.UtcNow,
            "status",
            "github-run-watch",
            "github-run-watch",
            "waiting",
            null,
            null,
            30,
            0,
            2,
            2,
            7,
            3,
            10,
            "healthy",
            "repairing drift",
            new StatusRollup(0, 0, 0, 0, 0, 1),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("repairing", "repairing drift"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            0,
            [],
            null,
            null,
            null,
            0,
            [],
            null,
            null,
            new InterventionTargetSnapshot(
                "github-replay-drift",
                "Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.",
                22,
                500,
                500,
                "backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs",
                "Summarize the persistent critical intervention target and the next operator action.",
                DateTimeOffset.UtcNow.AddHours(-2),
                "2h 0m old",
                "critical"),
            "Escalation: global intervention target is critical. Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.",
            4);

        var first = loop.EnqueuePersistentInterventionEscalationFollowUp(snapshot);
        var second = loop.EnqueuePersistentInterventionEscalationFollowUp(snapshot);

        Assert.NotNull(first);
        Assert.Null(second);
        Assert.Single(loop.ReadQueue());
    }

    [Fact]
    public void EnqueuePersistentInterventionEscalationFollowUp_RemovesStaleEscalationSummaryWhenTargetClears()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var staleJob = new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("Core", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["interventionEscalation"] = "true",
                ["interventionSignature"] = "github-replay-drift|22|500|500|||",
                ["workType"] = "operator-escalation"
            });
        new QueueStore(root).Enqueue(staleJob);

        var snapshot = new StatusSnapshot(
            DateTimeOffset.UtcNow,
            "status",
            "github-run-watch",
            "github-run-watch",
            "waiting",
            null,
            null,
            30,
            0,
            2,
            2,
            7,
            3,
            10,
            "healthy",
            "implementation work",
            new StatusRollup(0, 0, 0, 0, 1, 0),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("draining", "implementation work"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            0,
            [],
            null,
            null,
            null,
            0,
            [],
            null,
            null,
            new InterventionTargetSnapshot(
                "implementation",
                "Advance issue #22: refresh architecture docs.",
                22,
                null,
                null,
                "docs/ARCHITECTURE.md",
                "refresh architecture docs.",
                null,
                null,
                "fresh"),
            null,
            0);

        var result = loop.EnqueuePersistentInterventionEscalationFollowUp(snapshot);

        Assert.Null(result);
        Assert.Empty(loop.ReadQueue());
    }

    [Fact]
    public void EnqueuePersistentInterventionEscalationFollowUp_ReplacesStaleEscalationSummaryWhenSignatureChanges()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var staleJob = new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("Core", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["interventionEscalation"] = "true",
                ["interventionSignature"] = "github-replay-drift|22|500|500|||",
                ["workType"] = "operator-escalation"
            });
        new QueueStore(root).Enqueue(staleJob);

        var snapshot = new StatusSnapshot(
            DateTimeOffset.UtcNow,
            "status",
            "github-run-watch",
            "github-run-watch",
            "waiting",
            null,
            null,
            30,
            0,
            2,
            2,
            7,
            4,
            10,
            "healthy",
            "repairing drift",
            new StatusRollup(0, 0, 0, 0, 0, 1),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("repairing", "repairing drift"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            0,
            [],
            null,
            null,
            null,
            0,
            [],
            null,
            null,
            new InterventionTargetSnapshot(
                "github-replay-drift",
                "Recovery for issue #23 is active, but GitHub updates for recovery #501 are still queued for retry.",
                23,
                501,
                501,
                "backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs",
                "Summarize the persistent critical intervention target and the next operator action.",
                DateTimeOffset.UtcNow.AddHours(-2),
                "2h 0m old",
                "critical"),
            "Escalation: global intervention target is critical. Recovery for issue #23 is active, but GitHub updates for recovery #501 are still queued for retry.",
            4);

        var result = loop.EnqueuePersistentInterventionEscalationFollowUp(snapshot);

        Assert.NotNull(result);
        var queued = Assert.Single(loop.ReadQueue());
        Assert.Equal(23, queued.Issue);
        Assert.Equal("github-replay-drift|23|501|501|backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs|Summarize the persistent critical intervention target and the next operator action.", queued.Metadata["interventionSignature"]);
    }

    [Fact]
    public void EnqueuePersistentInterventionEscalationFollowUp_DoesNotRequeueAcknowledgedEscalation()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var signature = "github-replay-drift|22|500|500|backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs|Summarize the persistent critical intervention target and the next operator action.";
        var records = new ExecutionRecordStore(root);
        records.Append(
            new SelfBuildJob(
                "feedback",
                "summarize_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], null, null, null),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["interventionEscalation"] = "true",
                    ["interventionSignature"] = signature,
                    ["workType"] = "operator-escalation"
                }),
            new JobExecutionResult("job-1", "feedback", "success", "summarized", DateTimeOffset.UtcNow),
            []);

        var snapshot = new StatusSnapshot(
            DateTimeOffset.UtcNow,
            "status",
            "github-run-watch",
            "github-run-watch",
            "waiting",
            null,
            null,
            30,
            0,
            2,
            2,
            7,
            4,
            10,
            "healthy",
            "repairing drift",
            new StatusRollup(0, 0, 0, 0, 0, 1),
            null,
            null,
            null,
            new RecentLoopSignalSnapshot("repairing", "repairing drift"),
            "unknown",
            0,
            null,
            new StatusRollupDelta(0, 0, 0, 0),
            0,
            [],
            null,
            null,
            null,
            0,
            [],
            null,
            null,
            new InterventionTargetSnapshot(
                "github-replay-drift",
                "Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.",
                22,
                500,
                500,
                "backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs",
                "Summarize the persistent critical intervention target and the next operator action.",
                DateTimeOffset.UtcNow.AddHours(-2),
                "2h 0m old",
                "critical"),
            "Escalation: global intervention target is critical. Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.",
            4);

        var result = loop.EnqueuePersistentInterventionEscalationFollowUp(snapshot);

        Assert.Null(result);
        Assert.Empty(loop.ReadQueue());
    }

    [Fact]
    public void WriteStatus_IncludesLatestPassSummaryWhenProvided()
    {
        var root = CreateTempRoot();
        var outputPath = Path.Combine(root, "ui", "dragon-ui", "sample-status.json");
        var loop = new SelfBuildLoop(root);
        var latestPass = new LatestPassSummary(2, 4, 1, 3, true, false);

        var nextPollAt = DateTimeOffset.UtcNow.AddSeconds(30);
        var snapshot = loop.WriteStatus(outputPath, "run-polling", "polling", "waiting", null, nextPollAt, null, 0, 0, null, null, latestPass);

        Assert.NotNull(snapshot.LatestPass);
        Assert.Equal(2, snapshot.LatestPass!.PassNumber);
        Assert.Equal("run-polling", snapshot.LastCommand);
        Assert.Equal("polling", snapshot.WorkerMode);
        Assert.Equal("waiting", snapshot.WorkerState);
        Assert.Null(snapshot.WorkerCompletionReason);
        Assert.Equal(nextPollAt, snapshot.NextPollAt);
        Assert.Equal("poll-interval", snapshot.NextWakeReason);
        Assert.Null(snapshot.PollIntervalSeconds);
        Assert.Equal(0, snapshot.IdleStreak);
        Assert.Equal(0, snapshot.IdleTarget);
        Assert.Null(snapshot.IdlePassesRemaining);
        Assert.Null(snapshot.PassBudgetRemaining);

        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.Equal("run-polling", document.RootElement.GetProperty("lastCommand").GetString());
        Assert.Equal("polling", document.RootElement.GetProperty("workerMode").GetString());
        Assert.Equal("waiting", document.RootElement.GetProperty("workerState").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("workerCompletionReason").ValueKind);
        Assert.Equal(nextPollAt, document.RootElement.GetProperty("nextPollAt").GetDateTimeOffset());
        Assert.Equal("poll-interval", document.RootElement.GetProperty("nextWakeReason").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("pollIntervalSeconds").ValueKind);
        Assert.Equal(0, document.RootElement.GetProperty("idleStreak").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("idleTarget").GetInt32());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("idlePassesRemaining").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("passBudgetRemaining").ValueKind);
        var latestPassElement = document.RootElement.GetProperty("latestPass");
        Assert.Equal(2, latestPassElement.GetProperty("passNumber").GetInt32());
        Assert.Equal(4, latestPassElement.GetProperty("cycleCount").GetInt32());
        Assert.Equal(1, latestPassElement.GetProperty("seededCycles").GetInt32());
        Assert.Equal(3, latestPassElement.GetProperty("consumedCycles").GetInt32());
        Assert.True(latestPassElement.GetProperty("reachedIdle").GetBoolean());
        Assert.False(latestPassElement.GetProperty("reachedMaxCycles").GetBoolean());
    }

    [Fact]
    public void WriteStatus_IncludesPollIntervalSecondsWhenProvided()
    {
        var root = CreateTempRoot();
        var outputPath = Path.Combine(root, "ui", "dragon-ui", "sample-status.json");
        var loop = new SelfBuildLoop(root);

        var snapshot = loop.WriteStatus(outputPath, "run-watch", "watch", "waiting", "idle_target_reached", DateTimeOffset.UtcNow.AddSeconds(30), 15, 2, 3, 1, 5, null, 4, 9);

        Assert.Equal("run-watch", snapshot.LastCommand);
        Assert.Equal("watch", snapshot.WorkerMode);
        Assert.Equal("waiting", snapshot.WorkerState);
        Assert.Equal("idle_target_reached", snapshot.WorkerCompletionReason);
        Assert.Equal(15, snapshot.PollIntervalSeconds);
        Assert.Equal(2, snapshot.IdleStreak);
        Assert.Equal(3, snapshot.IdleTarget);
        Assert.Equal(1, snapshot.IdlePassesRemaining);
        Assert.Equal(5, snapshot.PassBudgetRemaining);
        Assert.Equal(4, snapshot.CurrentPassNumber);
        Assert.Equal(9, snapshot.MaxPasses);

        using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
        Assert.Equal("idle_target_reached", document.RootElement.GetProperty("workerCompletionReason").GetString());
        Assert.Equal(15, document.RootElement.GetProperty("pollIntervalSeconds").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("idleStreak").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("idleTarget").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("idlePassesRemaining").GetInt32());
        Assert.Equal(5, document.RootElement.GetProperty("passBudgetRemaining").GetInt32());
        Assert.Equal(4, document.RootElement.GetProperty("currentPassNumber").GetInt32());
        Assert.Equal(9, document.RootElement.GetProperty("maxPasses").GetInt32());
    }

    [Fact]
    public void RunUntilIdleFromGithub_RefreshesIssueListBetweenCycles()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        Directory.CreateDirectory(Path.Combine(root, "planning"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        File.WriteAllText(Path.Combine(root, "planning", "backlog.json"), """
        {
          "stories": [
            {
              "title": "[Story] Dragon Idea Engine Master Codex: Core System Principles",
              "heading": "Core System Principles",
              "sourceFile": "codex/sections/01-dragon-idea-engine-master-codex.md"
            }
          ]
        }
        """);

        var issueListCalls = 0;
        var service = new GithubIssueService((arguments, _) =>
        {
            if (arguments.StartsWith("issue list --repo", StringComparison.Ordinal))
            {
                issueListCalls += 1;
                return issueListCalls == 1
                    ? """
                    [
                      {
                        "number": 22,
                        "title": "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                        "body": "",
                        "state": "OPEN",
                        "labels": [
                          { "name": "story" }
                        ]
                      }
                    ]
                    """
                    : "[]";
            }

            return "[]";
        });

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var loop = new SelfBuildLoop(root, githubIssueService: service, jobExecutor: executor);

        var result = loop.RunUntilIdleFromGithub("tmassey1979", "IdeaEngine", maxCycles: 10);

        Assert.True(result.ReachedIdle);
        Assert.False(result.ReachedMaxCycles);
        Assert.True(issueListCalls >= 2);
        Assert.Equal(["seed", "consume", "consume", "consume"], result.Cycles.Select(cycle => cycle.Mode));
    }

    [Fact]
    public void RunUntilIdleFromGithub_CanSyncValidatedWorkflowAcrossMultipleCycles()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        Directory.CreateDirectory(Path.Combine(root, "planning"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        File.WriteAllText(Path.Combine(root, "planning", "backlog.json"), """
        {
          "stories": [
            {
              "title": "[Story] Dragon Idea Engine Master Codex: Core System Principles",
              "heading": "Core System Principles",
              "sourceFile": "codex/sections/01-dragon-idea-engine-master-codex.md"
            }
          ]
        }
        """);

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.StartsWith("issue list --repo", StringComparison.Ordinal))
            {
                return """
                [
                  {
                    "number": 22,
                    "title": "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                    "body": "",
                    "state": "OPEN",
                    "labels": [
                      { "name": "story" }
                    ]
                  }
                ]
                """;
            }

            return string.Empty;
        });

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var loop = new SelfBuildLoop(root, githubIssueService: service, jobExecutor: executor);

        var result = loop.RunUntilIdleFromGithub("tmassey1979", "IdeaEngine", syncValidatedWorkflows: true, maxCycles: 10);

        Assert.True(result.ReachedIdle);
        Assert.Contains(result.Cycles, cycle => cycle.GithubSync?.Updated == true);
        Assert.Contains(commands, command => command.Contains("issue comment 22", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue close 22", StringComparison.Ordinal));
    }

    [Fact]
    public void GithubIssueService_MapsOpenStoryIssuesAndBacklogMetadata()
    {
        const string json = """
        [
          {
            "number": 23,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "Implement the System Architecture portion.",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 5,
            "title": "Ignore epic",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "epic" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(23, issue.Number);
        Assert.Equal("System Architecture", issue.Heading);
        Assert.Contains("01-dragon-idea-engine-master-codex", issue.SourceFile, StringComparison.Ordinal);
    }

    [Fact]
    public void GithubIssueService_InfersSourceIssueForRecoveryIssue()
    {
        const string json = """
        [
          {
            "number": 500,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(22, issue.SourceIssueNumber);
    }

    [Fact]
    public void GithubIssueService_PrefersRecoveryTitleSourceIssueWhenBodyDisagrees()
    {
        const string json = """
        [
          {
            "number": 500,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue.\n\nContext:\n- source issue: #99",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(22, issue.SourceIssueNumber);
    }

    [Fact]
    public void GithubIssueService_UsesFirstParseableBodySourceIssueWhenBodyHasMultipleMatches()
    {
        const string json = """
        [
          {
            "number": 500,
            "title": "[Recovery] Core System Principles",
            "body": "Recovery story.\n\nContext:\n- source issue: #999999999999999999999\n- source issue: #22\n- source issue: #23",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(22, issue.SourceIssueNumber);
    }

    [Fact]
    public void GithubIssueService_UsesFirstParseableBodySourceIssueWhenMatchesRepeat()
    {
        const string json = """
        [
          {
            "number": 500,
            "title": "[Recovery] Core System Principles",
            "body": "Recovery story.\n\nContext:\n- source issue: #22\n- source issue: #22\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(22, issue.SourceIssueNumber);
    }

    [Fact]
    public void GithubIssueService_IgnoresInvalidRecoverySourceIssueNumbers()
    {
        const string json = """
        [
          {
            "number": 500,
            "title": "[Recovery] Issue #999999999999999999999: Core System Principles",
            "body": "Recovery story for quarantined issue.\n\nContext:\n- source issue: #999999999999999999999",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Null(issue.SourceIssueNumber);
    }

    [Fact]
    public void GithubIssueService_IgnoresSupersededRecoveryIssuesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 500,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" },
              { "name": "superseded" }
            ]
          },
          {
            "number": 501,
            "title": "[Recovery] Issue #22: Core System Principles Follow-up",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(501, issue.Number);
        Assert.Equal(22, issue.SourceIssueNumber);
    }

    [Fact]
    public void GithubIssueService_IgnoresOpenValidatedStoriesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 600,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "Validated but intentionally left open.",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "waiting-follow-up" }
            ]
          },
          {
            "number": 601,
            "title": "[Story] Dragon Idea Engine Master Codex: Autonomous Software Factory Loop",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(601, issue.Number);
    }

    [Fact]
    public void GithubIssueService_IgnoresValidatedLabelDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 601,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "Validated but stale label cleanup did not run yet.",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "validated" }
            ]
          },
          {
            "number": 602,
            "title": "[Story] Dragon Idea Engine Master Codex: Autonomous Software Factory Loop",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(602, issue.Number);
    }

    [Fact]
    public void GithubIssueService_IgnoresInProgressAndQuarantinedLabelsDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 603,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "Already in progress.",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "in-progress" }
            ]
          },
          {
            "number": 604,
            "title": "[Story] Dragon Idea Engine Master Codex: Core System Principles",
            "body": "Currently quarantined.",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "quarantined" }
            ]
          },
          {
            "number": 605,
            "title": "[Story] Dragon Idea Engine Master Codex: Autonomous Software Factory Loop",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(605, issue.Number);
    }

    [Fact]
    public void GithubIssueService_IgnoresWaitingFollowUpStateAfterDuplicateMerge()
    {
        const string json = """
        [
          {
            "number": 602,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 602,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "waiting-follow-up" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void GithubIssueService_IgnoresInProgressStateAfterDuplicateMerge()
    {
        const string json = """
        [
          {
            "number": 606,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 606,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "in-progress" },
              { "name": "stalled" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void GithubIssueService_IgnoresQuarantinedStateAfterDuplicateMerge()
    {
        const string json = """
        [
          {
            "number": 607,
            "title": "[Story] Dragon Idea Engine Master Codex: Core System Principles",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 607,
            "title": "[Story] Dragon Idea Engine Master Codex: Core System Principles",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "quarantined" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void GithubIssueService_IgnoresValidatedStateAfterDuplicateMerge()
    {
        const string json = """
        [
          {
            "number": 603,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 603,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "validated" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void GithubIssueService_IgnoresClosedStoriesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 700,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "CLOSED",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          },
          {
            "number": 701,
            "title": "[Story] Dragon Idea Engine Master Codex: Autonomous Software Factory Loop",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(701, issue.Number);
    }

    [Fact]
    public void GithubIssueService_IgnoresSupersededStateAfterDuplicateMerge()
    {
        const string json = """
        [
          {
            "number": 702,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          },
          {
            "number": 702,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" },
              { "name": "superseded" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void GithubIssueService_DeduplicatesStoriesByIssueNumberDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 800,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "First copy",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 800,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "Second copy",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(800, issue.Number);
    }

    [Fact]
    public void GithubIssueService_PrefersMostCompleteDuplicateIssueDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 804,
            "title": "",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 804,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "Richer duplicate entry",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(804, issue.Number);
        Assert.Equal("[Story] Dragon Idea Engine Master Codex: System Architecture", issue.Title);
        Assert.Equal("Richer duplicate entry", issue.Body);
        Assert.Equal("System Architecture", issue.Heading);
    }

    [Fact]
    public void GithubIssueService_MergesLabelsAcrossDuplicateIssuesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 809,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 809,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" },
              { "name": "backlog" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(809, issue.Number);
        Assert.Contains(issue.Labels, label => string.Equals(label, "story", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issue.Labels, label => string.Equals(label, "recovery", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issue.Labels, label => string.Equals(label, "backlog", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GithubIssueService_PreservesRecoverySourceIssueAcrossDuplicateIssuesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 810,
            "title": "[Recovery] Core System Principles",
            "body": "Recovery story without explicit source.",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          },
          {
            "number": 810,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(810, issue.Number);
        Assert.Equal(22, issue.SourceIssueNumber);
    }

    [Fact]
    public void GithubIssueService_PreservesBacklogMetadataAcrossDuplicateIssuesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 811,
            "title": "[Recovery] Custom duplicate without codex title",
            "body": "This duplicate has a richer body.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          },
          {
            "number": 811,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(811, issue.Number);
        Assert.Equal("[Recovery] Custom duplicate without codex title", issue.Title);
        Assert.Equal(22, issue.SourceIssueNumber);
        Assert.Equal("System Architecture", issue.Heading);
        Assert.Contains("01-dragon-idea-engine-master-codex", issue.SourceFile, StringComparison.Ordinal);
    }

    [Fact]
    public void GithubIssueService_PreservesDeterministicRecoverySourceAcrossDuplicateIssues()
    {
        const string json = """
        [
          {
            "number": 812,
            "title": "[Recovery] Issue #999: Core System Principles",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          },
          {
            "number": 812,
            "title": "[Recovery] Issue #22: Core System Principles",
            "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
            "state": "OPEN",
            "labels": [
              { "name": "story" },
              { "name": "recovery" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(812, issue.Number);
        Assert.Equal("[Recovery] Issue #22: Core System Principles", issue.Title);
        Assert.Equal(22, issue.SourceIssueNumber);
    }

    [Fact]
    public void GithubIssueService_PreservesDeterministicBacklogMetadataAcrossDuplicateIssues()
    {
        const string json = """
        [
          {
            "number": 813,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 813,
            "title": "[Story] Dragon Idea Engine Master Codex Addendum: AGENT JOB SCHEMA",
            "body": "Richer duplicate entry",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(813, issue.Number);
        Assert.Equal("[Story] Dragon Idea Engine Master Codex Addendum: AGENT JOB SCHEMA", issue.Title);
        Assert.Equal("AGENT_JOB_SCHEMA", issue.Heading);
        Assert.Contains("02-dragon-idea-engine-master-codex-addendum", issue.SourceFile, StringComparison.Ordinal);
    }

    [Fact]
    public void GithubIssueService_UsesDeterministicTieBreakForEquallyCompleteDuplicates()
    {
        const string json = """
        [
          {
            "number": 805,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "Zulu body",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          },
          {
            "number": 805,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "Alpha body",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(805, issue.Number);
        Assert.Equal("Alpha body", issue.Body);
    }

    [Fact]
    public void GithubIssueService_IgnoresMalformedEntriesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "title": "[Story] malformed entry missing number and labels",
            "state": "OPEN"
          },
          {
            "number": 801,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(801, issue.Number);
    }

    [Fact]
    public void GithubIssueService_NormalizesUnexpectedTitleAndBodyTypesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 806,
            "title": 123,
            "body": { "text": "not a string" },
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(806, issue.Number);
        Assert.Equal(string.Empty, issue.Title);
        Assert.Equal(string.Empty, issue.Body);
        Assert.Null(issue.Heading);
    }

    [Fact]
    public void GithubIssueService_IgnoresUnexpectedStateTypesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 807,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": 123,
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void GithubIssueService_IgnoresUnexpectedNumberShapesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 807.5,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void GithubIssueService_IgnoresMalformedLabelEntriesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 802,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              "story",
              { "name": "story" },
              null,
              42
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(802, issue.Number);
    }

    [Fact]
    public void GithubIssueService_IgnoresUnexpectedLabelNameTypesDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 808,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": 123 },
              { "name": true },
              { "name": "story" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(808, issue.Number);
        Assert.Single(issue.Labels);
        Assert.Equal("story", issue.Labels[0]);
    }

    [Fact]
    public void GithubIssueService_DeduplicatesMixedCaseLabelsDuringBacklogDiscovery()
    {
        const string json = """
        [
          {
            "number": 803,
            "title": "[Story] Dragon Idea Engine Master Codex: System Architecture",
            "body": "",
            "state": "OPEN",
            "labels": [
              { "name": "Story" },
              { "name": "story" },
              { "name": "STORY" }
            ]
          }
        ]
        """;

        var service = new GithubIssueService((_, _) => json);
        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        var issue = Assert.Single(issues);
        Assert.Equal(803, issue.Number);
        Assert.Single(issue.Labels);
        Assert.Contains(issue.Labels, label => string.Equals(label, "Story", StringComparison.Ordinal));
    }

    [Fact]
    public void GithubIssueService_TreatsBlankResponsesAsEmptyBacklog()
    {
        var service = new GithubIssueService((_, _) => "   ");

        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void GithubIssueService_TreatsMalformedJsonResponsesAsEmptyBacklog()
    {
        var service = new GithubIssueService((_, _) => "{not json");

        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void GithubIssueService_TreatsNonArrayResponsesAsEmptyBacklog()
    {
        var service = new GithubIssueService((_, _) => """{ "message": "not an array" }""");

        var issues = service.ListStoryIssues("tmassey1979", "IdeaEngine", FindRepoRoot());

        Assert.Empty(issues);
    }

    [Fact]
    public void SyncValidatedWorkflow_ClosesValidatedIssue()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        var records = new ExecutionRecordStore(root);
        store.Update(23, "System Architecture", "developer", new JobExecutionResult("job-1", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(23, "System Architecture", "review", new JobExecutionResult("job-2", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(23, "System Architecture", "test", new JobExecutionResult("job-3", "test", "success", "done", DateTimeOffset.UtcNow));
        records.Append(
            new SelfBuildJob(
                "developer",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                23,
                new SelfBuildJobPayload("System Architecture", ["story"], "System Architecture", "docs/ARCHITECTURE.md", null),
                new Dictionary<string, string> { ["changedPaths"] = "docs/ARCHITECTURE.md" }
            ),
            new JobExecutionResult("job-1", "developer", "success", "done", DateTimeOffset.UtcNow),
            []
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return string.Empty;
        });

        var loop = new SelfBuildLoop(root, githubIssueService: service);
        var result = loop.SyncValidatedWorkflow("tmassey1979", "IdeaEngine", 23);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("label create in-progress", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label quarantined", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label in-progress", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label validated", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label waiting-follow-up", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label superseded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue comment 23", StringComparison.Ordinal) && command.Contains("changed paths", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, command => command.Contains("issue close 23", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_UsesHeartbeatInsteadOfClosingWhenNoChangedPathsWereRecorded()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            102,
            "System Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["architect"] = new("success", "job-architect", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-review", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-test", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(102, "System Architecture", "architect", "implement_issue", "job-architect", "success", "done", DateTimeOffset.UtcNow, [], ["review"]),
            new ExecutionRecord(102, "System Architecture", "review", "review_issue", "job-review", "success", "done", DateTimeOffset.UtcNow, [], ["test"]),
            new ExecutionRecord(102, "System Architecture", "test", "test_issue", "job-test", "success", "done", DateTimeOffset.UtcNow, [], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/102/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.DoesNotContain(commands, command => command.Contains("issue close 102", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("dragon-backend-heartbeat", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("label create waiting-follow-up", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 102 --repo tmassey1979/IdeaEngine --add-label waiting-follow-up", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 102 --repo tmassey1979/IdeaEngine --remove-label in-progress", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label validated", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("auto-close: deferred because no execution-backed changed paths were recorded; waiting on follow-up", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_ToleratesLabelMutationPermissionFailure()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            102,
            "System Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["architect"] = new("success", "job-architect", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-review", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-test", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(102, "System Architecture", "architect", "implement_issue", "job-architect", "success", "done", DateTimeOffset.UtcNow, [], ["review"]),
            new ExecutionRecord(102, "System Architecture", "review", "review_issue", "job-review", "success", "done", DateTimeOffset.UtcNow, [], ["test"]),
            new ExecutionRecord(102, "System Architecture", "test", "test_issue", "job-test", "success", "done", DateTimeOffset.UtcNow, [], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue edit 102 --repo tmassey1979/IdeaEngine --add-label waiting-follow-up", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("gh command failed: failed to update https://github.com/tmassey1979/IdeaEngine/issues/102: GraphQL: Resource not accessible by personal access token (addLabelsToLabelable)");
            }

            return arguments.Contains("issues/102/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issue edit 102 --repo tmassey1979/IdeaEngine --add-label waiting-follow-up", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("dragon-backend-heartbeat", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("issue close 102", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncWorkflow_HeartbeatIncludesExecutionConflictNotes()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            110,
            "System Architecture",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-dev", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("pending", null, DateTimeOffset.UtcNow, null)
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(
                110,
                "System Architecture",
                "developer",
                "implement_issue",
                "job-dev",
                "success",
                "done",
                DateTimeOffset.UtcNow,
                ["docs/ARCHITECTURE.md"],
                ["review"],
                "Kept newer or higher-specificity same-artifact implementation; pruned weaker duplicates. Superseded implementation issues: 1005.")
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/110/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("execution notes: Kept newer or higher-specificity same-artifact implementation; pruned weaker duplicates. Superseded implementation issues: 1005.", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_UsesHeartbeatWhenChangedPathsAreBlank()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            103,
            "System Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-dev", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-review", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-test", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(103, "System Architecture", "developer", "implement_issue", "job-dev", "success", "done", DateTimeOffset.UtcNow, ["", "   "], ["review"]),
            new ExecutionRecord(103, "System Architecture", "review", "review_issue", "job-review", "success", "done", DateTimeOffset.UtcNow, [], ["test"]),
            new ExecutionRecord(103, "System Architecture", "test", "test_issue", "job-test", "success", "done", DateTimeOffset.UtcNow, [], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/103/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.DoesNotContain(commands, command => command.Contains("issue close 103", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 103 --repo tmassey1979/IdeaEngine --add-label waiting-follow-up", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("auto-close: deferred because no execution-backed changed paths were recorded; waiting on follow-up", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_TrimsChangedPathsBeforeClosing()
    {
        var root = FindRepoRoot();
        var workflow = new IssueWorkflowState(
            104,
            "System Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-dev", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-review", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-test", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(104, "System Architecture", "developer", "implement_issue", "job-dev", "success", "done", DateTimeOffset.UtcNow, [" docs/ARCHITECTURE.md ", "docs/ARCHITECTURE.md", "DOCS/ARCHITECTURE.md", "docs\\ARCHITECTURE.md", "./docs/ARCHITECTURE.md", "docs//ARCHITECTURE.md", "docs/./ARCHITECTURE.md", "docs/../docs/ARCHITECTURE.md", "./docs/../docs/ARCHITECTURE.md", "../outside.txt", "./docs/../../outside.txt", "/etc/passwd", "D:/elsewhere/outside.txt", "file:///tmp/outside.txt", "https://example.com/outside.txt", "file:outside.txt", "data:text/plain,hello", "docs/ARCHITECTURE.md?ref=runner", "docs/ARCHITECTURE.md#L10", "docs/ARCHITECTURE.md?ref=runner#L10", "docs/ARCHITECTURE.md&amp;ref=runner", "docs/ARCHITECTURE.md&#35;L10", "`docs/ARCHITECTURE.md`", "[docs/ARCHITECTURE.md]", "\"docs/ARCHITECTURE.md\"", "'docs/ARCHITECTURE.md'", "<docs/ARCHITECTURE.md>", "[Architecture](docs/ARCHITECTURE.md)", "[Architecture](docs/ARCHITECTURE.md \"doc\")", "[Architecture](docs/ARCHITECTURE.md 'doc')", "[Architecture](docs/ARCHITECTURE.md (doc))", "[Architecture](  docs/ARCHITECTURE.md  )", "[Architecture](  docs/ARCHITECTURE.md   \"doc\"  )", "[Architecture](docs/ARCHITECTURE.md#L10C2 \"doc\")", "[Architecture](<docs/ARCHITECTURE.md#L10C2> \"doc\")", "[Architecture](<docs%2FARCHITECTURE.md%23L10C2> \"doc\")", "[Architecture](<docs%2fARCHITECTURE.md%23L10C2> \"doc\")", "[Architecture](<docs%2FARCHITECTURE.md%3Fref%3Drunner> \"doc\")", "[Architecture](<docs%2fARCHITECTURE.md%3fref%3drunner%23L10C2> \"doc\")", "[Architecture](<docs%2FARCHITECTURE.md%3Fref%3Drunner%23L10C2> \"doc\")", "[Architecture](`docs/ARCHITECTURE.md`)", "[Architecture](\"docs/ARCHITECTURE.md\")", "[Architecture]('docs/ARCHITECTURE.md')", "[Architecture](`docs/ARCHITECTURE.md%23L10C2`)", "[Architecture](\"docs/ARCHITECTURE.md%23L10C2\")", "[Architecture]('<docs%2FARCHITECTURE.md%23L10C2>')", "[Architecture](<`docs/ARCHITECTURE.md%23L10C2`>)", "[Architecture](<'docs%2FARCHITECTURE.md%23L10C2'>)", "[Architecture](<\"docs%2FARCHITECTURE.md%23L10C2\">)", "[Architecture](<docs/ARCHITECTURE.md>)", "[Architecture](<docs/ARCHITECTURE.md> \"doc\")", "[Architecture](<docs%2FARCHITECTURE.md>)", "[Architecture](<docs%2fARCHITECTURE.md>)", "[Architecture](<docs%2FARCHITECTURE.md> \"doc\")", "docs/ARCHITECTURE%2Emd", "docs/ARCHITECTURE%2emd", "docs%2FARCHITECTURE.md", "docs%2fARCHITECTURE.md", "docs/ARCHITECTURE.md/.", "docs/ARCHITECTURE.md/", "/mnt/c/code/Playground/IdeaEngine/docs/ARCHITECTURE.md", "C:/code/Playground/IdeaEngine/docs/ARCHITECTURE.md"], ["review"]),
            new ExecutionRecord(104, "System Architecture", "review", "review_issue", "job-review", "success", "done", DateTimeOffset.UtcNow, [], ["test"]),
            new ExecutionRecord(104, "System Architecture", "test", "test_issue", "job-test", "success", "done", DateTimeOffset.UtcNow, [], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issue close 104", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("changed paths: docs/ARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("changed paths:  docs/ARCHITECTURE.md ", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("DOCS/ARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs\\\\ARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("./docs/ARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs//ARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/./ARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/../docs/ARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("./docs/../docs/ARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("../outside.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("./docs/../../outside.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("/etc/passwd", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("D:/elsewhere/outside.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("file:///tmp/outside.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("https://example.com/outside.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("file:outside.txt", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("data:text/plain,hello", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/ARCHITECTURE.md?ref=runner", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/ARCHITECTURE.md#L10", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/ARCHITECTURE.md?ref=runner#L10", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/ARCHITECTURE.md&amp;ref=runner", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/ARCHITECTURE.md&#35;L10", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("`docs/ARCHITECTURE.md`", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[docs/ARCHITECTURE.md]", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("\"docs/ARCHITECTURE.md\"", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("'docs/ARCHITECTURE.md'", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("<docs/ARCHITECTURE.md>", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](docs/ARCHITECTURE.md)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](docs/ARCHITECTURE.md \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](docs/ARCHITECTURE.md 'doc')", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](docs/ARCHITECTURE.md (doc))", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](  docs/ARCHITECTURE.md  )", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](  docs/ARCHITECTURE.md   \"doc\"  )", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](docs/ARCHITECTURE.md#L10C2 \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs/ARCHITECTURE.md#L10C2> \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2FARCHITECTURE.md%23L10C2> \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2fARCHITECTURE.md%23L10C2> \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2FARCHITECTURE.md%3Fref%3Drunner> \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2fARCHITECTURE.md%3fref%3drunner%23L10C2> \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2FARCHITECTURE.md%3Fref%3Drunner%23L10C2> \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](`docs/ARCHITECTURE.md`)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](\"docs/ARCHITECTURE.md\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture]('docs/ARCHITECTURE.md')", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](`docs/ARCHITECTURE.md%23L10C2`)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](\"docs/ARCHITECTURE.md%23L10C2\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture]('<docs%2FARCHITECTURE.md%23L10C2>')", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<`docs/ARCHITECTURE.md%23L10C2`>)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<'docs%2FARCHITECTURE.md%23L10C2'>)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<\"docs%2FARCHITECTURE.md%23L10C2\">)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs/ARCHITECTURE.md>)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs/ARCHITECTURE.md> \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2FARCHITECTURE.md>)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2fARCHITECTURE.md>)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2FARCHITECTURE.md> \"doc\")", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/ARCHITECTURE%2Emd", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/ARCHITECTURE%2emd", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs%2FARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs%2fARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/ARCHITECTURE.md/.", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/ARCHITECTURE.md/", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("/mnt/c/code/Playground/IdeaEngine/docs/ARCHITECTURE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("C:/code/Playground/IdeaEngine/docs/ARCHITECTURE.md", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_NormalizesEncodedSpacePathsBeforeClosing()
    {
        var root = FindRepoRoot();
        var workflow = new IssueWorkflowState(
            106,
            "Spaced Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-dev", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-review", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-test", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(
                106,
                "Spaced Architecture",
                "developer",
                "implement_issue",
                "job-dev",
                "success",
                "done",
                DateTimeOffset.UtcNow,
                [
                    "docs/My%20Architecture.md",
                    "[Architecture](docs/My%20Architecture.md)",
                    "[Architecture](<docs%2FMy%20Architecture.md>)",
                    "\"docs/My%20Architecture.md\"",
                    "`docs/My%20Architecture.md`"
                ],
                ["review"]),
            new ExecutionRecord(106, "Spaced Architecture", "review", "review_issue", "job-review", "success", "done", DateTimeOffset.UtcNow, [], ["test"]),
            new ExecutionRecord(106, "Spaced Architecture", "test", "test_issue", "job-test", "success", "done", DateTimeOffset.UtcNow, [], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issue close 106", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("changed paths: docs/My Architecture.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/My%20Architecture.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](docs/My%20Architecture.md)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2FMy%20Architecture.md>)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("\"docs/My%20Architecture.md\"", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("`docs/My%20Architecture.md`", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_NormalizesEncodedPunctuationPathsBeforeClosing()
    {
        var root = FindRepoRoot();
        var workflow = new IssueWorkflowState(
            107,
            "Punctuated Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-dev", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-review", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-test", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(
                107,
                "Punctuated Architecture",
                "developer",
                "implement_issue",
                "job-dev",
                "success",
                "done",
                DateTimeOffset.UtcNow,
                [
                    "docs/My%20Architecture%2BNotes.md",
                    "[Architecture](docs/My%20Architecture%2BNotes.md)",
                    "[Architecture](<docs%2FMy%20Architecture%2BNotes.md>)",
                    "\"docs/My%20Architecture%2BNotes.md\"",
                    "`docs/My%20Architecture%2BNotes.md`"
                ],
                ["review"]),
            new ExecutionRecord(107, "Punctuated Architecture", "review", "review_issue", "job-review", "success", "done", DateTimeOffset.UtcNow, [], ["test"]),
            new ExecutionRecord(107, "Punctuated Architecture", "test", "test_issue", "job-test", "success", "done", DateTimeOffset.UtcNow, [], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issue close 107", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("changed paths: docs/My Architecture+Notes.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/My%20Architecture%2BNotes.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](docs/My%20Architecture%2BNotes.md)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2FMy%20Architecture%2BNotes.md>)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("\"docs/My%20Architecture%2BNotes.md\"", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("`docs/My%20Architecture%2BNotes.md`", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_NormalizesEncodedParentheticalPathsBeforeClosing()
    {
        var root = FindRepoRoot();
        var workflow = new IssueWorkflowState(
            108,
            "Parenthetical Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-dev", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-review", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-test", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(
                108,
                "Parenthetical Architecture",
                "developer",
                "implement_issue",
                "job-dev",
                "success",
                "done",
                DateTimeOffset.UtcNow,
                [
                    "docs/Architecture%20(Draft)%2C%20v2.md",
                    "[Architecture](docs/Architecture%20(Draft)%2C%20v2.md)",
                    "[Architecture](<docs%2FArchitecture%20%28Draft%29%2C%20v2.md>)",
                    "\"docs/Architecture%20(Draft)%2C%20v2.md\"",
                    "`docs/Architecture%20(Draft)%2C%20v2.md`"
                ],
                ["review"]),
            new ExecutionRecord(108, "Parenthetical Architecture", "review", "review_issue", "job-review", "success", "done", DateTimeOffset.UtcNow, [], ["test"]),
            new ExecutionRecord(108, "Parenthetical Architecture", "test", "test_issue", "job-test", "success", "done", DateTimeOffset.UtcNow, [], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issue close 108", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("changed paths: docs/Architecture (Draft), v2.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/Architecture%20(Draft)%2C%20v2.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](docs/Architecture%20(Draft)%2C%20v2.md)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2FArchitecture%20%28Draft%29%2C%20v2.md>)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("\"docs/Architecture%20(Draft)%2C%20v2.md\"", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("`docs/Architecture%20(Draft)%2C%20v2.md`", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_NormalizesEncodedBracketedPathsBeforeClosing()
    {
        var root = FindRepoRoot();
        var workflow = new IssueWorkflowState(
            109,
            "Bracketed Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-dev", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-review", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-test", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(
                109,
                "Bracketed Architecture",
                "developer",
                "implement_issue",
                "job-dev",
                "success",
                "done",
                DateTimeOffset.UtcNow,
                [
                    "docs/Roadmap%20%5BPhase%20%232%5D.md",
                    "[Architecture](docs/Roadmap%20%5BPhase%20%232%5D.md)",
                    "[Architecture](<docs%2FRoadmap%20%5BPhase%20%232%5D.md>)",
                    "\"docs/Roadmap%20%5BPhase%20%232%5D.md\"",
                    "`docs/Roadmap%20%5BPhase%20%232%5D.md`"
                ],
                ["review"]),
            new ExecutionRecord(109, "Bracketed Architecture", "review", "review_issue", "job-review", "success", "done", DateTimeOffset.UtcNow, [], ["test"]),
            new ExecutionRecord(109, "Bracketed Architecture", "test", "test_issue", "job-test", "success", "done", DateTimeOffset.UtcNow, [], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issue close 109", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("changed paths: docs/Roadmap [Phase #2].md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("docs/Roadmap%20%5BPhase%20%232%5D.md", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](docs/Roadmap%20%5BPhase%20%232%5D.md)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("[Architecture](<docs%2FRoadmap%20%5BPhase%20%232%5D.md>)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("\"docs/Roadmap%20%5BPhase%20%232%5D.md\"", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("`docs/Roadmap%20%5BPhase%20%232%5D.md`", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_DoesNotCloseIssueWhenChangedPathsEscapeRepo()
    {
        var root = FindRepoRoot();
        var workflow = new IssueWorkflowState(
            105,
            "System Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-dev", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-review", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-test", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow
        );

        var records = new[]
        {
            new ExecutionRecord(105, "System Architecture", "developer", "implement_issue", "job-dev", "success", "done", DateTimeOffset.UtcNow, ["../outside.txt", "./docs/../../outside.txt", "/etc/passwd", "D:/elsewhere/outside.txt", "file:///tmp/outside.txt", "https://example.com/outside.txt", "file:outside.txt", "data:text/plain,hello"], ["review"]),
            new ExecutionRecord(105, "System Architecture", "review", "review_issue", "job-review", "success", "done", DateTimeOffset.UtcNow, [], ["test"]),
            new ExecutionRecord(105, "System Architecture", "test", "test_issue", "job-test", "success", "done", DateTimeOffset.UtcNow, [], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.DoesNotContain(commands, command => command.Contains("issue close 105", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("auto-close: deferred because no execution-backed changed paths were recorded", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncValidatedWorkflow_DoesNotCloseIssueWithActiveRecoveryChildren()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            23,
            "System Architecture",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", DateTimeOffset.UtcNow, "done"),
                ["review"] = new("success", "job-2", DateTimeOffset.UtcNow, "done"),
                ["test"] = new("success", "job-3", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow,
            null,
            null,
            [500]
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/23/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.DoesNotContain(commands, command => command.Contains("issue close 23", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery chain: current #23 -> children #500", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery state: active recovery children #500", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery writeback: clear", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker focus: draining recovery work", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("active recovery children: #500", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncInProgressWorkflow_ShowsReleasedRecoveryHoldWhenParentReturnsToActiveFlow()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow,
            "Recovery child completed; parent returned to active flow.",
            null,
            []
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return """
                [
                  {
                    "number": "bad",
                    "title": 123,
                    "body": [],
                    "labels": "broken"
                  },
                  {
                    "number": 500,
                    "title": "[Recovery] Issue #22: Core",
                    "body": "Recovery story for quarantined issue #22.\n\nContext:\n- source issue: #22",
                    "labels": [
                      { "name": "recovery" },
                      { "name": "story" }
                    ]
                  }
                ]
                """;
            }

            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issue close 500", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 500", StringComparison.Ordinal) && command.Contains("remove-label validated", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 500", StringComparison.Ordinal) && command.Contains("remove-label waiting-follow-up", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("retired recovery issues: #500", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery chain: current #22", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery hold: released; parent returned to active flow", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncInProgressWorkflow_ShowsRequeuedRecoveryHoldWhenParentIsResumed()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow,
            "Recovery child completed; parent requeued for active flow.",
            null,
            []
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("recovery chain: current #22", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery hold: released and parent requeued for active flow", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkflowStateStore_TracksActiveRecoveryChildren()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-parent", "developer", "failed", "blocked", DateTimeOffset.UtcNow));
        store.OverrideOverallStatus(22, "quarantined", "Parent is quarantined.");

        var recoveryJob = new SelfBuildJob(
            "developer",
            "recover_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            500,
            new SelfBuildJobPayload("[Recovery] Issue #22: Core", ["story", "recovery"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceIssueNumber"] = "22",
                ["workType"] = "recovery"
            }
        );

        store.Update(recoveryJob, new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));
        var withChild = store.ReadAll();
        Assert.Equal(22, withChild[500].SourceIssueNumber);
        Assert.Contains(500, withChild[22].ActiveRecoveryIssueNumbers!);

        store.Update(recoveryJob, new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(recoveryJob, new JobExecutionResult("job-test", "test", "success", "done", DateTimeOffset.UtcNow));
        var afterValidation = store.ReadAll();
        Assert.DoesNotContain(500, afterValidation[22].ActiveRecoveryIssueNumbers ?? []);
        Assert.Equal("in_progress", afterValidation[22].OverallStatus);
        Assert.Contains("Recovery child completed", afterValidation[22].Note, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncQuarantinedWorkflow_CommentsAndLabelsWithoutClosing()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        var records = new ExecutionRecordStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-1", "developer", "failed", "boom", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "review", new JobExecutionResult("job-2", "review", "failed", "boom", DateTimeOffset.UtcNow));
        var quarantined = store.OverrideOverallStatus(22, "quarantined", "Quarantined after repeated failures.");
        records.Append(
            new SelfBuildJob(
                "developer",
                "implement_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], "Core", "docs/ARCHITECTURE.md", null),
                new Dictionary<string, string> { ["changedPaths"] = "docs/ARCHITECTURE.md" }
            ),
            new JobExecutionResult("job-1", "developer", "failed", "boom", DateTimeOffset.UtcNow),
            []
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issue create --repo", StringComparison.Ordinal))
            {
                return "https://github.com/tmassey1979/IdeaEngine/issues/999";
            }

            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", quarantined, records.Read(22), root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("label create quarantined", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label in-progress", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label validated", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label waiting-follow-up", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 22", StringComparison.Ordinal) && command.Contains("add-label quarantined", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue create --repo tmassey1979/IdeaEngine", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("--label recovery", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("api repos/tmassey1979/IdeaEngine/issues/22/comments", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("--method POST", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("dragon-backend-remediation", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery chain: current #22", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery state: awaiting recovery path", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery writeback: clear", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker focus: draining recovery work", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery issue: #999", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("Recovery checklist", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("issue close 22", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncQuarantinedWorkflow_ToleratesMalformedCreatedRecoveryIssueUrl()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "quarantined",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("failed", "job-1", DateTimeOffset.UtcNow.AddMinutes(-30), "boom")
            },
            DateTimeOffset.UtcNow,
            "Quarantined after repeated failures."
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issue create --repo", StringComparison.Ordinal))
            {
                return "https://github.com/tmassey1979/IdeaEngine/issues/999999999999999999999";
            }

            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issue create --repo tmassey1979/IdeaEngine", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery issue: not created", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("issue close 22", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncQuarantinedWorkflow_ShowsRecoveryWritebackRetryWhenRecoveryChildSyncIsPending()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 23, 12, 30, 0, TimeSpan.Zero);
        Directory.CreateDirectory(Path.Combine(root, ".dragon", "status"));
        File.WriteAllText(
            Path.Combine(root, ".dragon", "status", "pending-github-sync.json"),
            """
            [
              {
                "issueNumber": 500,
                "summary": "GitHub sync failed for recovery issue #500.",
                "recordedAt": "2026-03-23T12:00:00Z",
                "attemptCount": 2,
                "lastAttemptedAt": "2026-03-23T12:01:00Z"
              }
            ]
            """);
        File.WriteAllText(
            Path.Combine(root, ".dragon", "status", "runtime-status.json"),
            """
            {
              "source": "status",
              "generatedAt": "2026-03-23T12:01:45Z",
              "lastCommand": "github-run-watch",
              "workerMode": "watch",
              "workerState": "waiting",
              "workerActivity": "Replaying pending GitHub updates before the next watch pass.",
              "leadJob": {
                "issueNumber": 500,
                "agent": "documentation",
                "action": "review_issue",
                "targetArtifact": "docs/generated/provider-notes.md",
                "targetOutcome": "stabilize provider notes recovery summary",
                "priority": "high",
                "blocking": true,
                "workType": "recovery"
              },
              "leadQuarantine": {
                "issueNumber": 22,
                "issueTitle": "Core",
                "note": "Quarantined after repeated failures.",
                "queuedRecoveryJobs": 1,
                "recoveryIssueNumber": 500,
                "recoveryIssueTitle": "[Recovery] Core",
                "state": "sync-drift",
                "summary": "Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.",
                "oldestPendingGithubSyncAt": "2026-03-23T12:00:00Z"
              },
              "pollIntervalSeconds": 30,
              "nextPollAt": "2026-03-23T12:31:00Z",
              "nextWakeReason": "delayed-provider-retry",
              "currentPassNumber": 4,
              "maxPasses": 9,
              "idleStreak": 2,
              "idleTarget": 3,
              "idlePassesRemaining": 1,
              "passBudgetRemaining": 5,
              "latestGithubSync": {
                "issueNumber": 500,
                "attempted": true,
                "updated": false,
                "summary": "GitHub sync failed for recovery issue #500.",
                "recordedAt": "2026-03-23T12:01:00Z"
              },
              "latestGithubReplay": {
                "attemptedCount": 3,
                "updatedCount": 3,
                "failedCount": 0,
                "summary": "Replayed 3 pending GitHub updates.",
                "recordedAt": "2026-03-23T12:01:30Z"
              },
              "pendingGithubSync": [
                {
                  "issueNumber": 500,
                  "summary": "GitHub sync failed for recovery issue #500.",
                  "recordedAt": "2026-03-23T12:00:00Z",
                  "attemptCount": 2,
                  "lastAttemptedAt": "2026-03-23T12:01:00Z"
                }
              ],
              "pendingGithubSyncCount": 1,
              "pendingGithubSyncSummary": "1 pending GitHub update remains queued for retry.",
              "latestActivity": {
                "issueNumber": 500,
                "issueTitle": "[Recovery] Core",
                "currentStage": "review",
                "summary": "Recovery review is waiting on GitHub writeback replay.",
                "recordedAt": "2026-03-23T12:01:40Z"
              },
              "rollup": {
                "failedIssues": 0,
                "quarantinedIssues": 1,
                "actionableQuarantinedIssues": 1,
                "inactiveQuarantinedIssues": 0,
                "inProgressIssues": 0,
                "validatedIssues": 0
              },
              "rollupDelta": {
                "failedIssues": 0,
                "quarantinedIssues": 1,
                "inProgressIssues": -1,
                "validatedIssues": 0
              },
              "queuedJobs": 1,
              "queueDirection": "down",
              "queueDelta": -2,
              "queueComparedAt": "2026-03-23T11:59:00Z",
              "health": "healthy",
              "attentionSummary": "3 GitHub update(s) were replayed on the latest pass and the worker is waiting for a quiet confirmation pass.",
              "latestPass": {
                "passNumber": 4,
                "cycleCount": 0,
                "seededCycles": 0,
                "consumedCycles": 0,
                "reachedIdle": false,
                "reachedMaxCycles": false,
                "githubReplayAttemptedCount": 3,
                "githubReplayUpdatedCount": 3,
                "githubReplayFailedCount": 0,
                "operatorEscalationQueuedCount": 0,
                "operatorEscalationConsumedCount": 0
              },
              "recentLoopSignal": {
                "mode": "repairing",
                "summary": "Replayed 3 pending GitHub updates and waiting for a quiet confirmation pass."
              },
              "interventionEscalationNote": "Escalation: global intervention target is critical. Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.",
              "interventionTarget": {
                "kind": "github-replay-drift",
                "summary": "Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.",
                "ageSummary": "4h 30m old",
                "escalation": "critical"
              }
            }
            """);

        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "quarantined",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("failed", "job-1", now.AddMinutes(-30), "boom")
            },
            now,
            "Quarantined after repeated failures.",
            null,
            [500]
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.DoesNotContain(commands, command => command.Contains("issue create --repo", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery writeback: retry pending for recovery child #500 (queued", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker focus: repairing GitHub writeback drift", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker command: github-run-watch", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker source: status", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker snapshot: 2026-03-23T12:01:45.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker snapshot age: 28m 15s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker mode: watch", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker state: waiting", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker activity: Replaying pending GitHub updates before the next watch pass.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead job: issue #500 · documentation:review_issue · docs/generated/provider-notes.md · stabilize provider notes recovery summary · priority high · blocking · recovery", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead quarantine: issue #22 · recovery #500 · 1 queued recovery job · sync-drift · Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead quarantine drift age: 30m 0s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker latest activity: issue #500 · stage review · Recovery review is waiting on GitHub writeback replay. · 2026-03-23T12:01:40.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker rollup: queued 1 · in-progress 0 · failed 0 · quarantined 1 (1 actionable, 0 inactive) · validated 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker rollup delta: failed +0 · quarantined +1 · in-progress -1 · validated +0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue trend: down · -2 · vs 2026-03-23T11:59:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue compared at: 2026-03-23T11:59:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue compare age: 2m 45s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker poll interval: 30s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker cadence: next wake 2026-03-23T12:31:00.0000000+00:00 (waiting for delayed provider retry; base poll every 30 seconds)", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake: 2026-03-23T12:31:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake reason: waiting for delayed provider retry", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake in: 1m 0s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker progress: pass 4 / 9 · idle 2 / 3 · remaining 1 · budget 5", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker completion: active", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync: issue #500 pending: GitHub sync failed for recovery issue #500.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync recorded at: 2026-03-23T12:01:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync age: 45s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay: 3/3 updated: Replayed 3 pending GitHub updates.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay recorded at: 2026-03-23T12:01:30.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay age: 15s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync count: 1", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync issues: #500", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync attempts: max 2", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync oldest queued at: 2026-03-23T12:00:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync oldest age: 1m 45s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync last attempt: 2026-03-23T12:01:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync next retry: not scheduled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync: 1 pending GitHub update remains queued for retry.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest pass: pass 4: 0 seed, 0 consume, replay 3/3", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest pass outcome: active", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker health: healthy", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker attention: 3 GitHub update(s) were replayed on the latest pass and the worker is waiting for a quiet confirmation pass.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker loop mode: repairing", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker loop summary: Replayed 3 pending GitHub updates and waiting for a quiet confirmation pass.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention target: github-replay-drift: Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry. (4h 30m old, critical)", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention age: 4h 30m old", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention escalation level: critical", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention acknowledged: no", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention acknowledged streak: 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("intervention escalation: Escalation: global intervention target is critical. Recovery for issue #22 is active, but GitHub updates for recovery #500 are still queued for retry.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("intervention escalation streak: 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("30m 0s ago", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncQuarantinedWorkflow_RetriesRecoveryIssueCreationWithoutRecoveryLabel_WhenLabelIsMissing()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "quarantined",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("failed", "job-1", DateTimeOffset.UtcNow.AddMinutes(-30), "boom")
            },
            DateTimeOffset.UtcNow,
            "Quarantined after repeated failures."
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issue create --repo", StringComparison.Ordinal) &&
                arguments.Contains("--label recovery", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("gh command failed: could not add label: 'recovery' not found");
            }

            if (arguments.Contains("issue create --repo", StringComparison.Ordinal))
            {
                return "https://github.com/tmassey1979/IdeaEngine/issues/999";
            }

            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("--label recovery", StringComparison.Ordinal));
        Assert.Contains(commands, command =>
            command.Contains("issue create --repo tmassey1979/IdeaEngine", StringComparison.Ordinal) &&
            command.Contains("--label story", StringComparison.Ordinal) &&
            command.Contains("--label backlog", StringComparison.Ordinal) &&
            !command.Contains("--label recovery", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery issue: #999", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncQuarantinedWorkflow_OmitsBlankChangedPathsFromRecoveryTrail()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "quarantined",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("failed", "job-1", DateTimeOffset.UtcNow.AddMinutes(-30), "boom")
            },
            DateTimeOffset.UtcNow,
            "Quarantined after repeated failures."
        );

        var records = new[]
        {
            new ExecutionRecord(22, "Core", "developer", "implement_issue", "job-1", "failed", "boom", DateTimeOffset.UtcNow, ["", "   "], [])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issue create --repo", StringComparison.Ordinal))
            {
                return "https://github.com/tmassey1979/IdeaEngine/issues/999";
            }

            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("changed paths: none recorded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SyncQuarantinedWorkflow_UpdatesExistingRemediationComment()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "quarantined",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("failed", "job-1", DateTimeOffset.UtcNow.AddMinutes(-30), "boom")
            },
            DateTimeOffset.UtcNow,
            "Quarantined after repeated failures."
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return """
                [
                  {
                    "number": "bad",
                    "title": "[Recovery] Issue #22: Core",
                    "labels": "broken"
                  },
                  {
                    "number": 321,
                    "title": "[Recovery] Issue #22: Core",
                    "labels": [
                      { "name": "recovery" }
                    ]
                  }
                ]
                """;
            }

            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? """
                [
                  { "id": "bad", "body": "<!-- dragon-backend-remediation --> wrong-shape" },
                  { "id": 77, "body": "<!-- dragon-backend-remediation --> old" }
                ]
                """
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.DoesNotContain(commands, command => command.Contains("issue create --repo", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issues/comments/77", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("--method PATCH", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery chain: current #22", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("blocked stage: developer", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery issue: #321", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncQuarantinedWorkflow_RecognizesExistingRecoveryIssueByTitleWithoutRecoveryLabel()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "quarantined",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("failed", "job-1", DateTimeOffset.UtcNow.AddMinutes(-30), "boom")
            },
            DateTimeOffset.UtcNow,
            "Quarantined after repeated failures."
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return """
                [
                  {
                    "number": 321,
                    "title": "[Recovery] Issue #22: Core",
                    "labels": [
                      { "name": "story" }
                    ]
                  }
                ]
                """;
            }

            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.DoesNotContain(commands, command => command.Contains("issue create --repo", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery issue: #321", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncQuarantinedWorkflow_ReusesActiveRecoveryChildInsteadOfCreatingAnother()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "quarantined",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("failed", "job-1", DateTimeOffset.UtcNow.AddMinutes(-30), "boom")
            },
            DateTimeOffset.UtcNow,
            "Quarantined after repeated failures.",
            null,
            [500, 501]
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.DoesNotContain(commands, command => command.Contains("issue create --repo", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("label create superseded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 500", StringComparison.Ordinal) && command.Contains("add-label superseded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 500", StringComparison.Ordinal) && command.Contains("remove-label validated", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 500", StringComparison.Ordinal) && command.Contains("remove-label waiting-follow-up", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("api repos/tmassey1979/IdeaEngine/issues/500/comments", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("dragon-backend-superseded", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("issue edit 501", StringComparison.Ordinal) && command.Contains("add-label superseded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery chain: current #22 -> children #500, #501", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery issue: #501", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncInProgressWorkflow_ShowsRecoveryChainForRecoveryChild()
    {
        var root = CreateTempRoot();
        var workflow = new IssueWorkflowState(
            500,
            "[Recovery] Issue #22: Core",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", DateTimeOffset.UtcNow, "done")
            },
            DateTimeOffset.UtcNow,
            null,
            22,
            []
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/500/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issue edit 500", StringComparison.Ordinal) && command.Contains("remove-label superseded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery chain: parent #22 -> current #500", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery state: active child issue for parent #22", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery writeback: clear", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncInProgressWorkflow_CreatesHeartbeatComment()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 16, 15, 30, 0, TimeSpan.Zero);
        Directory.CreateDirectory(Path.Combine(root, ".dragon", "status"));
        File.WriteAllText(
            Path.Combine(root, ".dragon", "status", "runtime-status.json"),
            """
            {
              "source": "status",
              "generatedAt": "2026-03-16T15:26:20Z",
              "lastCommand": "run-polling",
              "workerMode": "polling",
              "workerState": "running",
              "workerActivity": "Advancing queued implementation work for issue #22.",
              "leadJob": {
                "issueNumber": 22,
                "agent": "documentation",
                "action": "implement_issue",
                "targetArtifact": "docs/architecture.md",
                "targetOutcome": "refresh architecture docs",
                "priority": "medium",
                "blocking": false,
                "workType": "story"
              },
              "leadQuarantine": null,
              "currentPassNumber": 2,
              "maxPasses": 6,
              "idleStreak": 0,
              "idleTarget": 2,
              "idlePassesRemaining": 2,
              "passBudgetRemaining": 4,
              "latestGithubSync": {
                "issueNumber": 22,
                "attempted": true,
                "updated": true,
                "summary": "Updated GitHub heartbeat for issue #22.",
                "recordedAt": "2026-03-16T15:26:00Z"
              },
              "latestGithubReplay": {
                "attemptedCount": 0,
                "updatedCount": 0,
                "failedCount": 0,
                "summary": "No pending GitHub updates needed replay on the latest pass.",
                "recordedAt": "2026-03-16T15:26:15Z"
              },
              "pendingGithubSyncCount": 0,
              "pendingGithubSyncSummary": null,
              "latestActivity": {
                "issueNumber": 22,
                "issueTitle": "Core",
                "currentStage": "review",
                "summary": "Implementation advanced into review.",
                "recordedAt": "2026-03-16T15:26:18Z"
              },
              "rollup": {
                "failedIssues": 0,
                "quarantinedIssues": 0,
                "actionableQuarantinedIssues": 0,
                "inactiveQuarantinedIssues": 0,
                "inProgressIssues": 1,
                "validatedIssues": 0
              },
              "rollupDelta": {
                "failedIssues": 0,
                "quarantinedIssues": 0,
                "inProgressIssues": 1,
                "validatedIssues": 0
              },
              "queuedJobs": 1,
              "queueDirection": "flat",
              "queueDelta": 0,
              "queueComparedAt": "2026-03-16T15:20:00Z",
              "health": "healthy",
              "attentionSummary": "1 queued job(s), 1 issue(s) in progress.",
              "latestPass": {
                "passNumber": 2,
                "cycleCount": 4,
                "seededCycles": 1,
                "consumedCycles": 3,
                "reachedIdle": false,
                "reachedMaxCycles": false,
                "githubReplayAttemptedCount": 0,
                "githubReplayUpdatedCount": 0,
                "githubReplayFailedCount": 0,
                "operatorEscalationQueuedCount": 0,
                "operatorEscalationConsumedCount": 0
              },
              "recentLoopSignal": {
                "mode": "draining",
                "summary": "Loop is actively draining queued work after issue #22."
              },
              "interventionEscalationNote": null,
              "interventionTarget": {
                "kind": "implementation",
                "summary": "Advance issue #22: refresh architecture docs.",
                "escalation": "fresh"
              }
            }
            """);
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", now.AddMinutes(-5), "done")
            },
            now
        );
        var records = new[]
        {
            new ExecutionRecord(22, "Core", "developer", "implement_issue", "job-1", "success", "done", now.AddMinutes(-5), [], ["review", "test"])
        };

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, records, root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("label create in-progress", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label quarantined", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label waiting-follow-up", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("add-label in-progress", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("api repos/tmassey1979/IdeaEngine/issues/22/comments", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("--method POST", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("dragon-backend-heartbeat", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("current stage: review", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("current stage updated: unknown", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("stalled: no", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("stalled reason: none", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest outcome: developer success (done)", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker focus: shipping implementation work", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker command: run-polling", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker source: status", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker snapshot: 2026-03-16T15:26:20.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker snapshot age: 3m 40s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker mode: polling", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker state: running", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker activity: Advancing queued implementation work for issue #22.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead job: issue #22 · documentation:implement_issue · docs/architecture.md · refresh architecture docs · priority medium · story", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead quarantine: none active", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead quarantine drift age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker latest activity: issue #22 · stage review · Implementation advanced into review. · 2026-03-16T15:26:18.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker rollup: queued 1 · in-progress 1 · failed 0 · quarantined 0 (0 actionable, 0 inactive) · validated 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker rollup delta: failed +0 · quarantined +0 · in-progress +1 · validated +0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue trend: flat · +0 · vs 2026-03-16T15:20:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue compared at: 2026-03-16T15:20:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue compare age: 6m 20s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker poll interval: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker cadence: not scheduled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake: not scheduled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake reason: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake in: not scheduled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker progress: pass 2 / 6 · idle 0 / 2 · remaining 2 · budget 4", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker completion: active", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync: issue #22 updated: Updated GitHub heartbeat for issue #22.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync recorded at: 2026-03-16T15:26:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync age: 20s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay: 0/0 updated: No pending GitHub updates needed replay on the latest pass.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay recorded at: 2026-03-16T15:26:15.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay age: 5s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync count: 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync issues: none", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync attempts: none", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync oldest queued at: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync oldest age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync last attempt: not attempted", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync next retry: not scheduled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync: clear", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest pass: pass 2: 4 cycles, 1 seed, 3 consume", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest pass outcome: active", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker health: healthy", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker attention: 1 queued job(s), 1 issue(s) in progress.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker loop mode: draining", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker loop summary: Loop is actively draining queued work after issue #22.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention target: implementation: Advance issue #22: refresh architecture docs. (fresh)", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention escalation level: fresh", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention acknowledged: no", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention acknowledged streak: 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("intervention escalation: none", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("intervention escalation streak: 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest execution recorded: 2026-03-16T15:25:00.0000000+00:00 (5m 0s ago)", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("label create stalled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label stalled", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncInProgressWorkflow_UsesOperatorEscalationWorkerFocusWhenRuntimeStatusShowsIt()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 16, 15, 30, 0, TimeSpan.Zero);
        Directory.CreateDirectory(Path.Combine(root, ".dragon", "status"));
        File.WriteAllText(
            Path.Combine(root, ".dragon", "status", "runtime-status.json"),
            """
            {
              "source": "status",
              "generatedAt": "2026-03-16T15:30:35Z",
              "lastCommand": "github-run-watch",
              "workerMode": "watch",
              "workerState": "waiting",
              "workerActivity": "Tracking acknowledged operator escalation before the next watch pass.",
              "leadJob": {
                "issueNumber": 22,
                "agent": "developer",
                "action": "summarize_issue",
                "targetArtifact": "backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs",
                "targetOutcome": "Summarize the persistent critical intervention target and the next operator action.",
                "priority": "high",
                "blocking": true,
                "workType": "operator-escalation"
              },
              "leadQuarantine": {
                "issueNumber": 22,
                "issueTitle": "Core",
                "note": "Quarantined after repeated failures.",
                "queuedRecoveryJobs": 1,
                "recoveryIssueNumber": 500,
                "recoveryIssueTitle": "[Recovery] Core",
                "state": "recovery-active",
                "summary": "Recovery issue #500 is actively draining work for parent issue #22."
              },
              "pollIntervalSeconds": 30,
              "nextPollAt": "2026-03-16T15:31:00Z",
              "nextWakeReason": "poll-interval",
              "currentPassNumber": 7,
              "maxPasses": 10,
              "idleStreak": 1,
              "idleTarget": 2,
              "idlePassesRemaining": 1,
              "passBudgetRemaining": 3,
              "latestGithubSync": {
                "issueNumber": 22,
                "attempted": true,
                "updated": true,
                "summary": "Updated GitHub heartbeat for issue #22.",
                "recordedAt": "2026-03-16T15:30:30Z"
              },
              "latestGithubReplay": {
                "attemptedCount": 0,
                "updatedCount": 0,
                "failedCount": 0,
                "summary": "No pending GitHub updates needed replay on the latest pass.",
                "recordedAt": "2026-03-16T15:30:30Z"
              },
              "pendingGithubSyncCount": 0,
              "pendingGithubSyncSummary": null,
              "latestActivity": {
                "issueNumber": 22,
                "issueTitle": "Core",
                "currentStage": "review",
                "summary": "Operator escalation summary remains the active checkpoint.",
                "recordedAt": "2026-03-16T15:30:32Z"
              },
              "rollup": {
                "failedIssues": 0,
                "quarantinedIssues": 1,
                "actionableQuarantinedIssues": 1,
                "inactiveQuarantinedIssues": 0,
                "inProgressIssues": 1,
                "validatedIssues": 0
              },
              "rollupDelta": {
                "failedIssues": 0,
                "quarantinedIssues": 0,
                "inProgressIssues": 0,
                "validatedIssues": 0
              },
              "queuedJobs": 1,
              "queueDirection": "up",
              "queueDelta": 1,
              "queueComparedAt": "2026-03-16T15:25:00Z",
              "health": "attention",
              "attentionSummary": "Critical intervention target remains acknowledged but unresolved. Escalate issue #22: Summarize the persistent critical intervention target and the next operator action.",
              "latestPass": {
                "passNumber": 7,
                "cycleCount": 2,
                "seededCycles": 1,
                "consumedCycles": 1,
                "reachedIdle": false,
                "reachedMaxCycles": false,
                "githubReplayAttemptedCount": 0,
                "githubReplayUpdatedCount": 0,
                "githubReplayFailedCount": 0,
                "operatorEscalationQueuedCount": 1,
                "operatorEscalationConsumedCount": 1
              },
              "recentLoopSignal": {
                "mode": "monitoring",
                "summary": "Loop is tracking acknowledged operator escalation."
              },
              "interventionEscalationNote": "Escalation: global intervention target is critical. Summarize the persistent critical intervention target and the next operator action.",
              "interventionEscalationStreak": 3,
              "interventionTarget": {
                "kind": "operator-escalation",
                "summary": "Escalate issue #22: Summarize the persistent critical intervention target and the next operator action.",
                "escalation": "critical",
                "acknowledged": true,
                "acknowledgedStreak": 3
              }
            }
            """);
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", now.AddMinutes(-5), "done")
            },
            now
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("worker focus: tracking acknowledged operator escalation", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker command: github-run-watch", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker source: status", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker snapshot: 2026-03-16T15:30:35.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker snapshot age: 0s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker mode: watch", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker state: waiting", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker activity: Tracking acknowledged operator escalation before the next watch pass.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead job: issue #22 · developer:summarize_issue · backend/src/Dragon.Backend.Orchestrator/GithubIssueService.cs · Summarize the persistent critical intervention target and the next operator action. · priority high · blocking · operator-escalation", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead quarantine: issue #22 · recovery #500 · 1 queued recovery job · recovery-active · Recovery issue #500 is actively draining work for parent issue #22.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead quarantine drift age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker latest activity: issue #22 · stage review · Operator escalation summary remains the active checkpoint. · 2026-03-16T15:30:32.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker rollup: queued 1 · in-progress 1 · failed 0 · quarantined 1 (1 actionable, 0 inactive) · validated 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker rollup delta: failed +0 · quarantined +0 · in-progress +0 · validated +0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue trend: up · +1 · vs 2026-03-16T15:25:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue compared at: 2026-03-16T15:25:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue compare age: 5m 35s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker poll interval: 30s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker cadence: next wake 2026-03-16T15:31:00.0000000+00:00 (scheduled poll interval; base poll every 30 seconds)", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake: 2026-03-16T15:31:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake reason: scheduled poll interval", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake in: 1m 0s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker progress: pass 7 / 10 · idle 1 / 2 · remaining 1 · budget 3", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker completion: active", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync: issue #22 updated: Updated GitHub heartbeat for issue #22.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync recorded at: 2026-03-16T15:30:30.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync age: 5s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay: 0/0 updated: No pending GitHub updates needed replay on the latest pass.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay recorded at: 2026-03-16T15:30:30.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay age: 5s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync count: 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync issues: none", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync attempts: none", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync oldest queued at: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync oldest age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync last attempt: not attempted", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync next retry: not scheduled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync: clear", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest pass: pass 7: 1 seed, 1 consume, escalation 1/1", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest pass outcome: active", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker health: attention", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker attention: Critical intervention target remains acknowledged but unresolved.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker loop mode: monitoring", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker loop summary: Loop is tracking acknowledged operator escalation.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention target: operator-escalation: Escalate issue #22: Summarize the persistent critical intervention target and the next operator action. (critical, acknowledged x3)", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention escalation level: critical", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention acknowledged: yes", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention acknowledged streak: 3", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("intervention escalation streak: 3", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncInProgressWorkflow_ShowsWorkerCompletionReasonWhenRuntimeStatusCompleted()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 16, 15, 30, 0, TimeSpan.Zero);
        Directory.CreateDirectory(Path.Combine(root, ".dragon", "status"));
        File.WriteAllText(
            Path.Combine(root, ".dragon", "status", "runtime-status.json"),
            """
            {
              "source": "status",
              "generatedAt": "2026-03-16T15:30:01Z",
              "lastCommand": "run-watch",
              "workerMode": "watch",
              "workerState": "complete",
              "workerCompletionReason": "idle_target_reached",
              "workerActivity": "Worker stopped after reaching the configured idle target.",
              "leadJob": null,
              "leadQuarantine": null,
              "currentPassNumber": 6,
              "maxPasses": 6,
              "idleStreak": 2,
              "idleTarget": 2,
              "idlePassesRemaining": 0,
              "passBudgetRemaining": 0,
              "latestGithubSync": {
                "issueNumber": 22,
                "attempted": false,
                "updated": false,
                "summary": "GitHub sync skipped because no workflow updates were needed.",
                "recordedAt": "2026-03-16T15:30:00Z"
              },
              "latestGithubReplay": {
                "attemptedCount": 0,
                "updatedCount": 0,
                "failedCount": 0,
                "summary": "No pending GitHub updates needed replay on the latest pass.",
                "recordedAt": "2026-03-16T15:30:00Z"
              },
              "pendingGithubSyncCount": 0,
              "pendingGithubSyncSummary": null,
              "latestActivity": {
                "issueNumber": 22,
                "issueTitle": "Core",
                "currentStage": "complete",
                "summary": "Idle confirmation pass completed after validation.",
                "recordedAt": "2026-03-16T15:30:00Z"
              },
              "rollup": {
                "failedIssues": 0,
                "quarantinedIssues": 0,
                "actionableQuarantinedIssues": 0,
                "inactiveQuarantinedIssues": 0,
                "inProgressIssues": 0,
                "validatedIssues": 1
              },
              "rollupDelta": {
                "failedIssues": 0,
                "quarantinedIssues": 0,
                "inProgressIssues": -1,
                "validatedIssues": 1
              },
              "queuedJobs": 0,
              "queueDirection": "unknown",
              "queueDelta": 0,
              "queueComparedAt": null,
              "health": "idle",
              "attentionSummary": "No queued work and no active issue workflows.",
              "latestPass": {
                "passNumber": 6,
                "cycleCount": 3,
                "seededCycles": 1,
                "consumedCycles": 2,
                "reachedIdle": true,
                "reachedMaxCycles": false,
                "githubReplayAttemptedCount": 0,
                "githubReplayUpdatedCount": 0,
                "githubReplayFailedCount": 0,
                "operatorEscalationQueuedCount": 0,
                "operatorEscalationConsumedCount": 0
              },
              "recentLoopSignal": {
                "mode": "idle",
                "summary": "Loop completed with no immediate queued work."
              },
              "interventionEscalationNote": null,
              "interventionTarget": {
                "kind": "idle",
                "summary": "No immediate intervention target."
              }
            }
            """);
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "validated",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", now.AddMinutes(-5), "done")
            },
            now
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("worker snapshot: 2026-03-16T15:30:01.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker snapshot age: 0s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker state: complete", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker activity: Worker stopped after reaching the configured idle target.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead job: none queued", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead quarantine: none active", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker lead quarantine drift age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker latest activity: issue #22 · stage complete · Idle confirmation pass completed after validation. · 2026-03-16T15:30:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker rollup: queued 0 · in-progress 0 · failed 0 · quarantined 0 (0 actionable, 0 inactive) · validated 1", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker rollup delta: failed +0 · quarantined +0 · in-progress -1 · validated +1", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue trend: unknown · +0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue compared at: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker queue compare age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker poll interval: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake: not scheduled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake reason: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker next wake in: not scheduled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker progress: pass 6 / 6 · idle 2 / 2 · remaining 0 · budget 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker completion: idle target reached", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync: issue #22 not attempted: GitHub sync skipped because no workflow updates were needed.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync recorded at: 2026-03-16T15:30:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub sync age: 1s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay: 0/0 updated: No pending GitHub updates needed replay on the latest pass.", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay recorded at: 2026-03-16T15:30:00.0000000+00:00", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("GitHub replay age: 1s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync count: 0", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync issues: none", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync attempts: none", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync oldest queued at: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync oldest age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync last attempt: not attempted", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync next retry: not scheduled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("pending GitHub sync: clear", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest pass: pass 6: 3 cycles, 1 seed, 2 consume", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest pass outcome: idle reached", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker health: idle", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("worker loop mode: idle", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention age: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention escalation level: not recorded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention acknowledged: no", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("global intervention acknowledged streak: 0", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncInProgressWorkflow_UpdatesExistingHeartbeatComment()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 16, 15, 30, 0, TimeSpan.Zero);
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", now.AddMinutes(-3), "done")
            },
            now
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? """
                [
                  { "id": "bad", "body": "<!-- dragon-backend-heartbeat --> wrong-shape" },
                  { "id": 99, "body": "<!-- dragon-backend-heartbeat --> old" }
                ]
                """
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("issues/22/comments", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issues/comments/99", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("--method PATCH", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("current stage: review", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest execution recorded: none", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncInProgressWorkflow_IncludesCurrentStageTimestampWhenAvailable()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 16, 15, 30, 0, TimeSpan.Zero);
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", now.AddMinutes(-10), "done"),
                ["review"] = new("failed", "job-2", now.AddMinutes(-2), "reviewing")
            },
            now
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("current stage: review", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("current stage updated: 2026-03-16T15:28:00.0000000+00:00 (2m 0s ago)", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncInProgressWorkflow_FlagsStalledStageWhenThresholdIsExceeded()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 16, 15, 30, 0, TimeSpan.Zero);
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("failed", "job-1", now.AddMinutes(-20), "still failing")
            },
            now
        );

        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return arguments.Contains("issues/22/comments", StringComparison.Ordinal) && !arguments.Contains("--method POST", StringComparison.Ordinal)
                ? "[]"
                : string.Empty;
        });

        var result = service.SyncWorkflow("tmassey1979", "IdeaEngine", workflow, [], root);

        Assert.True(result.Attempted);
        Assert.True(result.Updated);
        Assert.Contains(commands, command => command.Contains("current stage: developer", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("stalled: yes", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("stalled reason: current stage has been idle for 20m 0s", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("add-label stalled", StringComparison.Ordinal));
    }

    [Fact]
    public void CycleOnce_CanAutomaticallySyncValidatedGithubWorkflow()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var commands = new List<string>();
        var service = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            return string.Empty;
        });
        var stories = new[]
        {
            new GithubIssue(
                22,
                "[Story] Dragon Idea Engine Master Codex: Core System Principles",
                "OPEN",
                ["story"],
                "",
                "Core System Principles",
                "codex/sections/01-dragon-idea-engine-master-codex.md"
            )
        };

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty));
        var loop = new SelfBuildLoop(root, githubIssueService: service, jobExecutor: executor);
        loop.CycleOnce(stories, repo: "IdeaEngine", project: "DragonIdeaEngine", githubOwner: "tmassey1979", syncValidatedWorkflows: true);
        loop.CycleOnce(stories, repo: "IdeaEngine", project: "DragonIdeaEngine", githubOwner: "tmassey1979", syncValidatedWorkflows: true);
        loop.CycleOnce(stories, repo: "IdeaEngine", project: "DragonIdeaEngine", githubOwner: "tmassey1979", syncValidatedWorkflows: true);
        var result = loop.CycleOnce(stories, repo: "IdeaEngine", project: "DragonIdeaEngine", githubOwner: "tmassey1979", syncValidatedWorkflows: true);

        Assert.NotNull(result.GithubSync);
        Assert.True(result.GithubSync!.Attempted);
        Assert.True(result.GithubSync.Updated);
        Assert.NotNull(result.ExecutionRecord);
        Assert.Contains(commands, command => command.Contains("issue comment 22", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue close 22", StringComparison.Ordinal));
    }

    [Fact]
    public void ReviewAndTestExecutors_UseChangedPathsAndRealCommands()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "docs", "ARCHITECTURE.md"), "# Architecture");
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "node -e \"process.exit(0)\"" } }""");

        var store = new WorkflowStateStore(root);
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(22, "Core", "review", new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));

        var executed = new List<string>();
        var executor = new LocalJobExecutor((fileName, arguments, _) =>
        {
            executed.Add($"{fileName} {arguments}");
            return new CommandResult(0, "ok", string.Empty);
        });

        var reviewResult = executor.Execute(
            root,
            new SelfBuildJob(
                "review",
                "review_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], "Core", "docs/ARCHITECTURE.md", null),
                new Dictionary<string, string> { ["changedPaths"] = "docs/ARCHITECTURE.md" }
            )
        );
        var testResult = executor.Execute(
            root,
            new SelfBuildJob(
                "test",
                "test_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                22,
                new SelfBuildJobPayload("Core", ["story"], "Core", "docs/ARCHITECTURE.md", null),
                new Dictionary<string, string>()
            )
        );

        Assert.Equal("success", reviewResult.Status);
        Assert.Equal("success", testResult.Status);
        Assert.Single(executed);
        Assert.Contains("npm", executed[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReviewAndTestExecutors_AcceptSuccessfulArchitectStageWithoutChangedPaths()
    {
        var root = CreateTempRoot();
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "node -e \"process.exit(0)\"" } }""");

        var store = new WorkflowStateStore(root);
        store.Update(102, "System Architecture", "architect", new JobExecutionResult("job-architect", "architect", "success", "done", DateTimeOffset.UtcNow));
        store.Update(102, "System Architecture", "review", new JobExecutionResult("job-review", "review", "success", "done", DateTimeOffset.UtcNow));

        var executed = new List<string>();
        var executor = new LocalJobExecutor((fileName, arguments, _) =>
        {
            executed.Add($"{fileName} {arguments}");
            return new CommandResult(0, "ok", string.Empty);
        });

        var reviewResult = executor.Execute(
            root,
            new SelfBuildJob(
                "review",
                "review_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                102,
                new SelfBuildJobPayload("System Architecture", ["story"], "System Architecture", "docs/ARCHITECTURE.md", null),
                new Dictionary<string, string>()
            )
        );
        var testResult = executor.Execute(
            root,
            new SelfBuildJob(
                "test",
                "test_issue",
                "IdeaEngine",
                "DragonIdeaEngine",
                102,
                new SelfBuildJobPayload("System Architecture", ["story"], "System Architecture", "docs/ARCHITECTURE.md", null),
                new Dictionary<string, string>()
            )
        );

        Assert.Equal("success", reviewResult.Status);
        Assert.Contains("architect", reviewResult.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("success", testResult.Status);
        Assert.Single(executed);
    }

    [Fact]
    public void WorkflowStateStore_ValidatesSuccessfulArchitectReviewAndTestStages()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        var now = DateTimeOffset.UtcNow;

        store.Update(102, "System Architecture", "architect", new JobExecutionResult("job-architect", "architect", "success", "done", now));
        store.Update(102, "System Architecture", "review", new JobExecutionResult("job-review", "review", "success", "done", now.AddSeconds(1)));
        var workflow = store.Update(102, "System Architecture", "test", new JobExecutionResult("job-test", "test", "success", "done", now.AddSeconds(2)));

        Assert.Equal("validated", workflow.OverallStatus);
    }

    [Fact]
    public void FailurePolicy_QuarantinesAfterThreeConsecutiveFailures()
    {
        var now = DateTimeOffset.UtcNow;
        var records = new[]
        {
            new ExecutionRecord(22, "Core", "developer", "implement_issue", "job-1", "failed", "boom", now.AddMinutes(-2), [], []),
            new ExecutionRecord(22, "Core", "developer", "implement_issue", "job-2", "failed", "boom", now.AddMinutes(-1), [], []),
            new ExecutionRecord(22, "Core", "developer", "implement_issue", "job-3", "failed", "boom", now, [], [])
        };

        var disposition = FailurePolicy.Evaluate(records);

        Assert.True(disposition.Quarantined);
        Assert.Contains("3 repeated failed developer executions", disposition.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void FailurePolicy_IdentifiesTransientModelProviderPressure()
    {
        var now = DateTimeOffset.UtcNow;
        var records = new[]
        {
            new ExecutionRecord(22, "Core", "architect", "implement_issue", "job-1", "failed", "Transient model provider failure from openai-responses (HTTP 429, retry after 30s): OpenAI Responses request failed with HTTP 429 (Too Many Requests).", now.AddMinutes(-2), [], []),
            new ExecutionRecord(22, "Core", "architect", "implement_issue", "job-2", "failed", "Transient model provider failure from openai-responses (HTTP 429, retry after 30s): OpenAI Responses request failed with HTTP 429 (Too Many Requests).", now.AddMinutes(-1), [], []),
            new ExecutionRecord(22, "Core", "architect", "implement_issue", "job-3", "failed", "Transient model provider failure from openai-responses (HTTP 429, retry after 30s): OpenAI Responses request failed with HTTP 429 (Too Many Requests).", now, [], [])
        };

        var disposition = FailurePolicy.Evaluate(records);

        Assert.True(disposition.Quarantined);
        Assert.Contains("transient model provider pressure", disposition.Reason, StringComparison.Ordinal);
        Assert.Contains("HTTP 429", disposition.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void FailurePolicy_QuarantinesLongStalledWorkflow()
    {
        var now = new DateTimeOffset(2026, 3, 16, 15, 30, 0, TimeSpan.Zero);
        var workflow = new IssueWorkflowState(
            22,
            "Core",
            "in_progress",
            new Dictionary<string, WorkflowStageState>
            {
                ["developer"] = new("success", "job-1", now.AddHours(-2), "done"),
                ["review"] = new("failed", "job-2", now.AddHours(-2), "blocked")
            },
            now
        );

        var disposition = FailurePolicy.Evaluate(workflow, now);

        Assert.True(disposition.Quarantined);
        Assert.Contains("prolonged stall in review", disposition.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void CycleOnce_QuarantinesRepeatedlyFailingIssueAndSkipsReseedingIt()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var stories = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"], "", "Core System Principles", "codex/sections/01-dragon-idea-engine-master-codex.md"),
            new GithubIssue(23, "[Story] Dragon Idea Engine Master Codex: System Architecture", "OPEN", ["story"], "", "System Architecture", "codex/sections/01-dragon-idea-engine-master-codex.md")
        };

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(1, string.Empty, "forced failure"));
        var loop = new SelfBuildLoop(root, jobExecutor: executor);

        loop.CycleOnce(stories);
        loop.CycleOnce(stories);
        loop.CycleOnce(stories);
        var firstFailure = loop.CycleOnce(stories);
        loop.CycleOnce(stories);
        loop.CycleOnce(stories);
        loop.CycleOnce(stories);
        var secondFailure = loop.CycleOnce(stories);
        loop.CycleOnce(stories);
        loop.CycleOnce(stories);
        loop.CycleOnce(stories);
        var thirdFailure = loop.CycleOnce(stories);
        var nextSeed = loop.CycleOnce(stories);

        Assert.Equal("failed", firstFailure.Workflow!.OverallStatus);
        Assert.Equal("failed", secondFailure.Workflow!.OverallStatus);
        Assert.NotNull(thirdFailure.FailureDisposition);
        Assert.True(thirdFailure.FailureDisposition!.Quarantined);

        var statePath = Path.Combine(root, ".dragon", "state", "issues.json");
        var state = File.ReadAllText(statePath);
        Assert.Contains("quarantined", state, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("seed", nextSeed.Mode);
        Assert.Equal(23, nextSeed.Job!.Issue);
    }

    [Fact]
    public void CycleOnce_RequeuesTransientProviderFailures_WithoutCountingThemAsOrdinaryFailures()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var stories = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Architect Agent", "OPEN", ["story"], "", "Architect Agent", "codex/sections/01-dragon-idea-engine-master-codex.md")
        };
        var provider = new SequencedPlannerAgentModelProvider(
            new AgentModelProviderException(
                "openai-responses",
                "OpenAI Responses request failed with HTTP 429 (Too Many Requests).",
                true,
                HttpStatusCode.TooManyRequests,
                TimeSpan.FromSeconds(7)),
            new AgentModelResponse("fake", "gpt-5", "resp_ok", "Recovered after queue retry.", "completed"));
        var executor = new LocalJobExecutor(
            (_, _, _) => new CommandResult(0, "ok", string.Empty),
            provider,
            new ModelExecutionRetryOptions(MaxAttempts: 1, BaseDelayMilliseconds: 0),
            _ => { });
        var loop = new SelfBuildLoop(root, jobExecutor: executor, nowProvider: () => now);

        var seeded = loop.CycleOnce(stories);
        var retried = loop.CycleOnce(stories);

        Assert.Equal("seed", seeded.Mode);
        Assert.Equal("retry", retried.Mode);
        Assert.Equal("failed", retried.Execution!.Status);
        Assert.Equal("in_progress", retried.Workflow!.OverallStatus);
        Assert.Contains("requeued for retry (1/2)", retried.Workflow.Note, StringComparison.Ordinal);
        Assert.Null(retried.ExecutionRecord);
        Assert.Null(retried.FailureDisposition);

        var queuedAfterRetry = loop.ReadQueue();
        var queuedRetryJob = Assert.Single(queuedAfterRetry);
        Assert.Equal("architect", queuedRetryJob.Agent);
        Assert.Equal("1", queuedRetryJob.Metadata["transientProviderRetryCount"]);
        Assert.Equal(retried.Execution.RetryNotBefore?.ToString("O", System.Globalization.CultureInfo.InvariantCulture), queuedRetryJob.Metadata["retryNotBeforeUtc"]);

        var waiting = loop.CycleOnce(stories);
        Assert.Equal("waiting", waiting.Mode);

        now = retried.Execution.RetryNotBefore!.Value.AddSeconds(1);
        var recovered = loop.CycleOnce(stories);

        Assert.Equal("consume", recovered.Mode);
        Assert.Equal("success", recovered.Execution!.Status);
        Assert.Equal("in_progress", recovered.Workflow!.OverallStatus);
        Assert.Contains(loop.ReadQueue(), job => job.Agent == "review");
        Assert.Contains(loop.ReadQueue(), job => job.Agent == "test");
    }

    [Fact]
    public void RunUntilIdle_DoesNotReportIdle_WhenOnlyDelayedRetryWorkRemains()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);
        var queue = new QueueStore(root, nowProvider: () => now);
        queue.Enqueue(new SelfBuildJob(
            "architect",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            22,
            new SelfBuildJobPayload("[Story] Delayed retry", ["story"], "Architect Agent", "codex/sections/01-dragon-idea-engine-master-codex.md", null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["retryNotBeforeUtc"] = now.AddMinutes(5).ToString("O", System.Globalization.CultureInfo.InvariantCulture)
            }));
        var loop = new SelfBuildLoop(root, nowProvider: () => now);

        var result = loop.RunUntilIdle([]);

        Assert.False(result.ReachedIdle);
        Assert.False(result.ReachedMaxCycles);
        Assert.Empty(result.Cycles);
    }

    [Fact]
    public void CycleOnce_QuarantinesLongStalledWorkflowBeforeSeedingNewWork()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        var now = DateTimeOffset.UtcNow;
        store.Update(22, "Core", "developer", new JobExecutionResult("job-dev", "developer", "success", "done", now.AddHours(-2)));
        store.Update(22, "Core", "review", new JobExecutionResult("job-review", "review", "failed", "blocked", now.AddHours(-2)));

        var commands = new List<string>();
        var github = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issue create --repo", StringComparison.Ordinal))
            {
                return "https://github.com/tmassey1979/IdeaEngine/issues/998";
            }

            return string.Empty;
        });
        var loop = new SelfBuildLoop(root, githubIssueService: github);
        var stories = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"]),
            new GithubIssue(23, "[Story] Dragon Idea Engine Master Codex: System Architecture", "OPEN", ["story"])
        };

        var result = loop.CycleOnce(stories, repo: "IdeaEngine", project: "DragonIdeaEngine", githubOwner: "tmassey1979", syncValidatedWorkflows: true);

        Assert.Equal("quarantine", result.Mode);
        Assert.NotNull(result.Workflow);
        Assert.Equal("quarantined", result.Workflow!.OverallStatus);
        Assert.NotNull(result.FailureDisposition);
        Assert.True(result.FailureDisposition!.Quarantined);
        Assert.Contains("prolonged stall", result.FailureDisposition.Reason, StringComparison.Ordinal);
        Assert.NotNull(result.GithubSync);
        Assert.True(result.GithubSync!.Updated);
        Assert.Contains(commands, command => command.Contains("dragon-backend-remediation", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue create --repo tmassey1979/IdeaEngine", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("issue close 22", StringComparison.Ordinal));
    }

    [Fact]
    public void CycleOnce_CanSyncQuarantinedWorkflowToGithub()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var stories = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"], "", "Core System Principles", "codex/sections/01-dragon-idea-engine-master-codex.md"),
            new GithubIssue(23, "[Story] Dragon Idea Engine Master Codex: System Architecture", "OPEN", ["story"], "", "System Architecture", "codex/sections/01-dragon-idea-engine-master-codex.md")
        };

        var commands = new List<string>();
        var github = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issue create --repo", StringComparison.Ordinal))
            {
                return "https://github.com/tmassey1979/IdeaEngine/issues/997";
            }

            return string.Empty;
        });
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(1, string.Empty, "forced failure"));
        var loop = new SelfBuildLoop(root, githubIssueService: github, jobExecutor: executor);

        CycleResult? quarantineCycle = null;
        for (var index = 0; index < 12; index += 1)
        {
            var cycle = loop.CycleOnce(stories, repo: "IdeaEngine", project: "DragonIdeaEngine", githubOwner: "tmassey1979", syncValidatedWorkflows: true);
            if (cycle.FailureDisposition?.Quarantined == true)
            {
                quarantineCycle = cycle;
                break;
            }
        }

        Assert.NotNull(quarantineCycle);
        Assert.NotNull(quarantineCycle.GithubSync);
        Assert.True(quarantineCycle.GithubSync!.Attempted);
        Assert.True(quarantineCycle.GithubSync.Updated);
        Assert.Contains(commands, command => command.Contains("dragon-backend-remediation", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue create --repo tmassey1979/IdeaEngine", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue edit 22", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("issue close 22", StringComparison.Ordinal));
    }

    [Fact]
    public void CycleOnce_ToleratesGithubSyncFailure()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), """{ "scripts": { "test": "placeholder" } }""");
        var stories = new[]
        {
            new GithubIssue(22, "[Story] Dragon Idea Engine Master Codex: Core System Principles", "OPEN", ["story"], "", "Core System Principles", "codex/sections/01-dragon-idea-engine-master-codex.md"),
            new GithubIssue(23, "[Story] Dragon Idea Engine Master Codex: System Architecture", "OPEN", ["story"], "", "System Architecture", "codex/sections/01-dragon-idea-engine-master-codex.md")
        };

        var commands = new List<string>();
        var github = new GithubIssueService((arguments, _) =>
        {
            commands.Add(arguments);
            if (arguments.Contains("issue list --repo", StringComparison.Ordinal))
            {
                return "[]";
            }

            if (arguments.Contains("issues/22/comments", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("gh command failed: gh: Resource not accessible by personal access token (HTTP 403)");
            }

            return string.Empty;
        });
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(1, string.Empty, "forced failure"));
        var loop = new SelfBuildLoop(root, githubIssueService: github, jobExecutor: executor);

        CycleResult? quarantineCycle = null;
        for (var index = 0; index < 12; index += 1)
        {
            var cycle = loop.CycleOnce(stories, repo: "IdeaEngine", project: "DragonIdeaEngine", githubOwner: "tmassey1979", syncValidatedWorkflows: true);
            if (cycle.FailureDisposition?.Quarantined == true)
            {
                quarantineCycle = cycle;
                break;
            }
        }

        Assert.NotNull(quarantineCycle);
        Assert.NotNull(quarantineCycle!.GithubSync);
        Assert.True(quarantineCycle.GithubSync!.Attempted);
        Assert.False(quarantineCycle.GithubSync.Updated);
        Assert.Contains("GitHub sync failed", quarantineCycle.GithubSync.Summary, StringComparison.Ordinal);
        Assert.Contains(commands, command => command.Contains("issues/22/comments", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var backlogPath = Path.Combine(current.FullName, "planning", "backlog.json");
            if (File.Exists(backlogPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root from test output directory.");
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragon-backend-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static int CountOccurrences(string value, string needle)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(needle))
        {
            return 0;
        }

        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count += 1;
            index += needle.Length;
        }

        return count;
    }

    private static string CreateLocalHttpPrefix()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}/";
    }

    private sealed class SequencedPlannerAgentModelProvider : IAgentModelProvider
    {
        private readonly Queue<object> outcomes;

        public SequencedPlannerAgentModelProvider(params object[] outcomes)
        {
            this.outcomes = new Queue<object>(outcomes);
        }

        public AgentModelProviderDescriptor Describe() =>
            new("fake", "memory", "gpt-5", "OPENAI_API_KEY", "planner test provider");

        public Task<AgentModelResponse> GenerateAsync(AgentModelRequest request, CancellationToken cancellationToken = default)
        {
            var next = outcomes.Count > 0
                ? outcomes.Dequeue()
                : new AgentModelResponse("fake", request.Model, "resp_default", "No output configured.", "completed");

            return next switch
            {
                AgentModelResponse response => Task.FromResult(response),
                Exception exception => Task.FromException<AgentModelResponse>(exception),
                _ => Task.FromException<AgentModelResponse>(new InvalidOperationException("Unsupported planner test provider outcome."))
            };
        }
    }
}
