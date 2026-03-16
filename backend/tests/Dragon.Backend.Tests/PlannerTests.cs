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
    public void CycleOnce_SeedsThenExecutesDeveloperFlow()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        File.WriteAllText(Path.Combine(root, "package.json"), "{}");
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

        var loop = new SelfBuildLoop(root);
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
        Assert.True(File.Exists(statePath));
        var state = File.ReadAllText(statePath);
        Assert.Contains("validated", state, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Core System Principles", File.ReadAllText(Path.Combine(root, "docs", "ARCHITECTURE.md")), StringComparison.Ordinal);
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
    public void SyncValidatedWorkflow_ClosesValidatedIssue()
    {
        var root = CreateTempRoot();
        var store = new WorkflowStateStore(root);
        store.Update(23, "System Architecture", "developer", new JobExecutionResult("job-1", "developer", "success", "done", DateTimeOffset.UtcNow));
        store.Update(23, "System Architecture", "review", new JobExecutionResult("job-2", "review", "success", "done", DateTimeOffset.UtcNow));
        store.Update(23, "System Architecture", "test", new JobExecutionResult("job-3", "test", "success", "done", DateTimeOffset.UtcNow));

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
        Assert.Equal(2, commands.Count);
        Assert.Contains("issue comment 23", commands[0], StringComparison.Ordinal);
        Assert.Contains("issue close 23", commands[1], StringComparison.Ordinal);
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
