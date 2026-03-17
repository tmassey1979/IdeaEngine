using Dragon.Backend.Contracts;
using Dragon.Backend.Orchestrator;

namespace Dragon.Backend.Tests;

public sealed class AgentModelExecutionTests
{
    [Fact]
    public void BuildPrompt_CreatesArchitectRequestFromJobContext()
    {
        var issue = new GithubIssue(
            310,
            "[Story] Dragon Idea Engine Master Codex: Architect Agent",
            "OPEN",
            ["story"],
            "Design the architecture for the agent runtime.",
            "Architect Agent",
            "codex/sections/01-dragon-idea-engine-master-codex.md"
        );
        var job = SelfBuildJobFactory.Create(issue, "architect", "IdeaEngine", "DragonIdeaEngine");

        var request = AgentPromptFactory.Build(job);

        Assert.Equal("architect", request.Agent);
        Assert.Equal("implement_issue", request.Purpose);
        Assert.Equal("gpt-5", request.Model);
        Assert.Contains("architect agent", request.Instructions!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(request.Messages, message => message.Role == "user" && message.Content.Contains(issue.Title, StringComparison.Ordinal));
        Assert.Equal("310", request.Metadata!["issueNumber"]);
    }

    [Fact]
    public void Execute_UsesModelProviderForArchitectJobs()
    {
        var issue = new GithubIssue(
            310,
            "[Story] Dragon Idea Engine Master Codex: Architect Agent",
            "OPEN",
            ["story"],
            "Design the architecture for the agent runtime.",
            "Architect Agent",
            "codex/sections/01-dragon-idea-engine-master-codex.md"
        );
        var job = SelfBuildJobFactory.Create(issue, "architect", "IdeaEngine", "DragonIdeaEngine");
        var provider = new FakeAgentModelProvider();
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);

        var result = executor.Execute(CreateTempRoot(), job);

        Assert.Equal("success", result.Status);
        Assert.Equal("Architect response from provider.", result.Summary);
        Assert.NotNull(provider.LastRequest);
        Assert.Equal("architect", provider.LastRequest!.Agent);
        Assert.Equal("implement_issue", provider.LastRequest.Purpose);
    }

    [Fact]
    public void SeedNext_SelectsDocumentationAgentForDocumentationStories()
    {
        var root = CreateTempRoot();
        var loop = new SelfBuildLoop(root);
        var issues = new[]
        {
            new GithubIssue(417, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        var job = loop.SeedNext(issues);

        Assert.Equal("documentation", job.Agent);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dragon-agent-model-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FakeAgentModelProvider : IAgentModelProvider
    {
        public AgentModelRequest? LastRequest { get; private set; }

        public AgentModelProviderDescriptor Describe() =>
            new("fake", "memory", "gpt-5", "OPENAI_API_KEY", "test provider");

        public Task<AgentModelResponse> GenerateAsync(AgentModelRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new AgentModelResponse("fake", request.Model, "resp_test", "Architect response from provider.", "completed"));
        }
    }
}
