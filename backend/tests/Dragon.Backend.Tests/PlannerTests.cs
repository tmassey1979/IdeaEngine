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
        Assert.Equal(1, queue.Dequeue()!.Issue);
        Assert.Equal(2, queue.Dequeue()!.Issue);
        Assert.Null(queue.Dequeue());
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
        Assert.Contains(commands, command => command.Contains("remove-label superseded", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue comment 23", StringComparison.Ordinal) && command.Contains("changed paths", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, command => command.Contains("issue close 23", StringComparison.Ordinal));
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
        Assert.Contains(commands, command => command.Contains("issue edit 22", StringComparison.Ordinal) && command.Contains("add-label quarantined", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("issue create --repo tmassey1979/IdeaEngine", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("--label recovery", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("api repos/tmassey1979/IdeaEngine/issues/22/comments", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("--method POST", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("dragon-backend-remediation", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery chain: current #22", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("recovery issue: #999", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("Recovery checklist", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("issue close 22", StringComparison.Ordinal));
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
                ? """[{ "id": 77, "body": "<!-- dragon-backend-remediation --> old" }]"""
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
    }

    [Fact]
    public void SyncInProgressWorkflow_CreatesHeartbeatComment()
    {
        var root = CreateTempRoot();
        var now = new DateTimeOffset(2026, 3, 16, 15, 30, 0, TimeSpan.Zero);
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
        Assert.Contains(commands, command => command.Contains("add-label in-progress", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("api repos/tmassey1979/IdeaEngine/issues/22/comments", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("--method POST", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("dragon-backend-heartbeat", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("current stage: review", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("current stage updated: unknown", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("stalled: no", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("stalled reason: none", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest outcome: developer success (done)", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("latest execution recorded: 2026-03-16T15:25:00.0000000+00:00 (5m 0s ago)", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("label create stalled", StringComparison.Ordinal));
        Assert.Contains(commands, command => command.Contains("remove-label stalled", StringComparison.Ordinal));
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
                ? """[{ "id": 99, "body": "<!-- dragon-backend-heartbeat --> old" }]"""
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
}
