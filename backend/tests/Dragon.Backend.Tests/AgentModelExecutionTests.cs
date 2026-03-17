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
        Assert.Contains("Return JSON only", request.Instructions!, StringComparison.OrdinalIgnoreCase);
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
    public void Execute_UsesStructuredAgentResultSummary_WhenProviderReturnsJson()
    {
        var issue = new GithubIssue(
            418,
            "[Story] Dragon Idea Engine Master Codex: Documentation Agent",
            "OPEN",
            ["story"]
        );
        var job = SelfBuildJobFactory.Create(issue, "documentation", "IdeaEngine", "DragonIdeaEngine");
        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Documentation plan generated.",
              "recommendation": "Update operator docs before enabling unattended mode.",
              "artifacts": ["docs/OPENAI_PROVIDER.md", "docs/AUTONOMY_ROADMAP.md"]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);

        var result = executor.Execute(CreateTempRoot(), job);

        Assert.Equal("success", result.Status);
        Assert.Equal("Documentation plan generated.", result.Summary);
    }

    [Fact]
    public void Execute_AppliesStructuredOperations_FromModelBackedAgent()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        var issue = new GithubIssue(
            419,
            "[Story] Dragon Idea Engine Master Codex: Documentation Agent",
            "OPEN",
            ["story"]
        );
        var job = SelfBuildJobFactory.Create(issue, "documentation", "IdeaEngine", "DragonIdeaEngine");
        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Documentation updated.",
              "artifacts": ["docs/generated/provider-notes.md"],
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nAPI-first execution enabled.\n"
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);

        var result = executor.Execute(root, job);

        Assert.Equal("success", result.Status);
        Assert.Equal("Documentation updated.", result.Summary);
        Assert.Equal(["docs/generated/provider-notes.md"], result.ChangedPaths);
        Assert.Contains("API-first execution enabled.", File.ReadAllText(Path.Combine(root, "docs", "generated", "provider-notes.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void ParseStructuredResult_ReadsJsonAgentOutput()
    {
        var parsed = AgentStructuredResultParser.Parse(
            """
            {
              "summary": "Architecture direction updated.",
              "recommendation": "Use API-backed providers first.",
              "artifacts": ["docs/ARCHITECTURE.md"],
              "operations": [
                {
                  "type": "append_text",
                  "path": "docs/ARCHITECTURE.md",
                  "content": "\nUpdated."
                }
              ]
            }
            """
        );

        Assert.NotNull(parsed);
        Assert.Equal("Architecture direction updated.", parsed!.Summary);
        Assert.Equal("Use API-backed providers first.", parsed.Recommendation);
        Assert.NotNull(parsed.Artifacts);
        Assert.Single(parsed.Artifacts!);
        Assert.NotNull(parsed.Operations);
        Assert.Single(parsed.Operations!);
    }

    [Fact]
    public void ParseStructuredResult_ReturnsNullForPlainText()
    {
        var parsed = AgentStructuredResultParser.Parse("plain text result");

        Assert.Null(parsed);
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

    [Fact]
    public void CreateDefault_LeavesModelBackedAgentsInBootstrapModeWithoutApiKey()
    {
        var issue = new GithubIssue(
            310,
            "[Story] Dragon Idea Engine Master Codex: Architect Agent",
            "OPEN",
            ["story"]
        );
        var job = SelfBuildJobFactory.Create(issue, "architect", "IdeaEngine", "DragonIdeaEngine");
        var executor = LocalJobExecutor.CreateDefault(_ => null, (_, _, _) => new CommandResult(0, "ok", string.Empty));

        var result = executor.Execute(CreateTempRoot(), job);

        Assert.Equal("success", result.Status);
        Assert.Contains("No model provider configured", result.Summary, StringComparison.Ordinal);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dragon-agent-model-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FakeAgentModelProvider : IAgentModelProvider
    {
        private readonly string outputText;

        public FakeAgentModelProvider(string outputText = "Architect response from provider.")
        {
            this.outputText = outputText;
        }

        public AgentModelRequest? LastRequest { get; private set; }

        public AgentModelProviderDescriptor Describe() =>
            new("fake", "memory", "gpt-5", "OPENAI_API_KEY", "test provider");

        public Task<AgentModelResponse> GenerateAsync(AgentModelRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new AgentModelResponse("fake", request.Model, "resp_test", outputText, "completed"));
        }
    }
}
