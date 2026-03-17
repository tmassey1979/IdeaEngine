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
    public void Execute_CarriesStructuredFollowUpRequests_FromModelBackedAgent()
    {
        var root = CreateTempRoot();
        var issue = new GithubIssue(
            420,
            "[Story] Dragon Idea Engine Master Codex: Documentation Agent",
            "OPEN",
            ["story"]
        );
        var job = SelfBuildJobFactory.Create(issue, "documentation", "IdeaEngine", "DragonIdeaEngine");
        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Documentation updated.",
              "followUps": [
                {
                  "agent": "feedback",
                  "action": "summarize_issue",
                  "priority": "high",
                  "reason": "Operator summary should be generated immediately.",
                  "targetArtifact": "docs/generated/provider-notes.md"
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);

        var result = executor.Execute(root, job);

        Assert.NotNull(result.RequestedFollowUps);
        Assert.Single(result.RequestedFollowUps!);
        Assert.Equal("feedback", result.RequestedFollowUps![0].Agent);
        Assert.Equal("high", result.RequestedFollowUps[0].Priority);
        Assert.Equal("docs/generated/provider-notes.md", result.RequestedFollowUps[0].TargetArtifact);
        Assert.Contains("Operator summary", result.RequestedFollowUps[0].Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void CycleOnce_QueuesRequestedModelFollowUps_AlongsideReviewAndTest()
    {
        var root = CreateTempRoot();
        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Documentation updated.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nQueued extra follow-up.\n"
                }
              ],
              "followUps": [
                {
                  "agent": "feedback",
                  "action": "summarize_issue",
                  "priority": "high",
                  "reason": "Summarize the documentation change for operators.",
                  "targetArtifact": "docs/generated/provider-notes.md"
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(421, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        var result = loop.CycleOnce(issues);

        Assert.Equal("consume", result.Mode);
        Assert.Equal(3, result.FollowUps.Count);
        Assert.Contains(result.FollowUps, job => job.Agent == "review" && job.Action == "review_issue");
        Assert.Contains(result.FollowUps, job => job.Agent == "test" && job.Action == "test_issue");
        Assert.Contains(result.FollowUps, job => job.Agent == "feedback" && job.Action == "summarize_issue");
        var feedbackFollowUp = Assert.Single(result.FollowUps, job => job.Agent == "feedback");
        Assert.Equal("high", feedbackFollowUp.Metadata["requestedPriority"]);
        Assert.Contains("documentation change", feedbackFollowUp.Metadata["requestedReason"], StringComparison.Ordinal);
        Assert.Equal("docs/generated/provider-notes.md", feedbackFollowUp.Metadata["targetArtifact"]);
    }

    [Fact]
    public void CycleOnce_OrdersRequestedModelFollowUps_ByPriorityAfterReviewAndTest()
    {
        var root = CreateTempRoot();
        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Documentation updated.",
              "followUps": [
                {
                  "agent": "repository-manager",
                  "action": "summarize_issue",
                  "priority": "low",
                  "reason": "Repository note can wait."
                },
                {
                  "agent": "feedback",
                  "action": "summarize_issue",
                  "priority": "high",
                  "reason": "Operator summary should run first."
                },
                {
                  "agent": "architect",
                  "action": "summarize_issue",
                  "priority": "normal",
                  "reason": "Architecture summary is useful but not urgent."
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(422, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        var result = loop.CycleOnce(issues);

        Assert.Equal(
            ["review", "test", "feedback", "architect", "repository-manager"],
            result.FollowUps.Select(job => job.Agent).ToArray());
        Assert.Equal("high", result.FollowUps[2].Metadata["requestedPriority"]);
        Assert.Equal("normal", result.FollowUps[3].Metadata["requestedPriority"]);
        Assert.Equal("low", result.FollowUps[4].Metadata["requestedPriority"]);
    }

    [Fact]
    public void CycleOnce_PrioritizesValidationAndHighPriorityFollowUps_AheadOfQueuedBacklogWork()
    {
        var root = CreateTempRoot();
        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Documentation updated.",
              "followUps": [
                {
                  "agent": "feedback",
                  "action": "summarize_issue",
                  "priority": "high",
                  "reason": "Operator summary should run before other backlog work."
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var firstIssue = new GithubIssue(423, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"]);
        var secondIssue = new GithubIssue(424, "[Story] Dragon Idea Engine Master Codex: Repository Structure", "OPEN", ["story"]);

        loop.CycleOnce([firstIssue]);
        loop.SeedNext([secondIssue]);
        loop.CycleOnce([firstIssue, secondIssue]);
        var next = loop.CycleOnce([firstIssue, secondIssue]);

        Assert.NotNull(next.Job);
        Assert.Equal("review", next.Job!.Agent);
        Assert.Equal("review_issue", next.Job.Action);
    }

    [Fact]
    public void CycleOnce_PrioritizesBlockingFollowUps_AheadOfOrdinaryBacklogWork()
    {
        var root = CreateTempRoot();
        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Documentation updated.",
              "followUps": [
                {
                  "agent": "feedback",
                  "action": "summarize_issue",
                  "priority": "normal",
                  "reason": "Operator summary must happen before new backlog work.",
                  "blocking": true
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var firstIssue = new GithubIssue(425, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"]);
        var secondIssue = new GithubIssue(426, "[Story] Dragon Idea Engine Master Codex: Repository Structure", "OPEN", ["story"]);

        loop.CycleOnce([firstIssue]);
        loop.SeedNext([secondIssue]);
        loop.CycleOnce([firstIssue, secondIssue]);
        loop.CycleOnce([firstIssue, secondIssue]);
        loop.CycleOnce([firstIssue, secondIssue]);
        var next = loop.CycleOnce([firstIssue, secondIssue]);

        Assert.NotNull(next.Job);
        Assert.Equal("feedback", next.Job!.Agent);
        Assert.Equal("summarize_issue", next.Job.Action);
        Assert.Equal("true", next.Job.Metadata["requestedBlocking"]);
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
              ],
              "followUps": [
                {
                  "agent": "feedback",
                  "action": "summarize_issue",
                  "priority": "high",
                  "reason": "Operators need a concise summary.",
                  "blocking": true,
                  "targetArtifact": "docs/ARCHITECTURE.md"
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
        Assert.NotNull(parsed.FollowUps);
        Assert.Single(parsed.FollowUps!);
        Assert.Equal("high", parsed.FollowUps[0].Priority);
        Assert.True(parsed.FollowUps[0].Blocking);
        Assert.Equal("docs/ARCHITECTURE.md", parsed.FollowUps[0].TargetArtifact);
        Assert.Contains("Operators need", parsed.FollowUps[0].Reason!, StringComparison.Ordinal);
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
