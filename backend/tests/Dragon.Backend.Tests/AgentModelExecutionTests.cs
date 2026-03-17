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
    public void BuildPrompt_IncludesTargetArtifactHint_ForRequestedFollowUpJobs()
    {
        var job = new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            427,
            new SelfBuildJobPayload(
                "[Story] Dragon Idea Engine Master Codex: Feedback Agent",
                ["story"],
                "Feedback Agent",
                "codex/sections/01-dragon-idea-engine-master-codex.md",
                null
            ),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["requestedReason"] = "Summarize the exact artifact that changed.",
                ["targetOutcome"] = "Produce an operator-facing summary of the updated provider notes."
            }
        );

        var request = AgentPromptFactory.Build(job);
        var userPrompt = Assert.Single(request.Messages, message => message.Role == "user").Content;

        Assert.Contains("Target artifact: docs/generated/provider-notes.md", userPrompt, StringComparison.Ordinal);
        Assert.Contains("Requested reason: Summarize the exact artifact that changed.", userPrompt, StringComparison.Ordinal);
        Assert.Contains("Target outcome: Produce an operator-facing summary of the updated provider notes.", userPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPrompt_IncludesChangedArtifactRollup_ForOperatorSummaryJobs()
    {
        var job = new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            447,
            new SelfBuildJobPayload(
                "[Story] Dragon Idea Engine Master Codex: Documentation Agent",
                ["story"],
                "Documentation Agent",
                "codex/sections/01-dragon-idea-engine-master-codex.md",
                null
            ),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Summarize the broader operator impact of the targeted implementation.",
                ["changedArtifactRollup"] = "docs/generated/provider-notes.md|docs/generated/provider-rollup.md"
            }
        );

        var request = AgentPromptFactory.Build(job);
        var userPrompt = Assert.Single(request.Messages, message => message.Role == "user").Content;

        Assert.Contains("Changed artifact rollup: docs/generated/provider-notes.md, docs/generated/provider-rollup.md", userPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildPrompt_StrengthensFeedbackInstructions_ForTargetedFollowUpJobs()
    {
        var job = new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            428,
            new SelfBuildJobPayload(
                "[Story] Dragon Idea Engine Master Codex: Feedback Agent",
                ["story"],
                "Feedback Agent",
                "codex/sections/01-dragon-idea-engine-master-codex.md",
                null
            ),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Produce an operator-facing summary of the updated provider notes."
            }
        );

        var request = AgentPromptFactory.Build(job);

        Assert.Contains("center the summary on that scoped work before broader observations", request.Instructions!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPrompt_StrengthensFeedbackInstructions_ForBroadRollupSummaryJobs()
    {
        var job = new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            448,
            new SelfBuildJobPayload(
                "[Story] Dragon Idea Engine Master Codex: Feedback Agent",
                ["story"],
                "Feedback Agent",
                "codex/sections/01-dragon-idea-engine-master-codex.md",
                null
            ),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                ["targetOutcome"] = "Summarize the broader operator impact of the targeted implementation.",
                ["changedArtifactRollup"] = "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs|backend/tests/Dragon.Backend.Tests/PromptFactoryTests.cs"
            }
        );

        var request = AgentPromptFactory.Build(job);

        Assert.Contains("explain the broader impact across the changed artifact rollup", request.Instructions!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPrompt_StrengthensDocumentationInstructions_ForTargetedFollowUpJobs()
    {
        var job = new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            429,
            new SelfBuildJobPayload(
                "[Story] Dragon Idea Engine Master Codex: Documentation Agent",
                ["story"],
                "Documentation Agent",
                "codex/sections/01-dragon-idea-engine-master-codex.md",
                null
            ),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Update the provider notes with a clearer operator explanation."
            }
        );

        var request = AgentPromptFactory.Build(job);

        Assert.Contains("treat that scoped artifact and outcome as the primary documentation surface", request.Instructions!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPrompt_StrengthensDocumentationInstructions_ForBroadRollupSummaryJobs()
    {
        var job = new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            449,
            new SelfBuildJobPayload(
                "[Story] Dragon Idea Engine Master Codex: Documentation Agent",
                ["story"],
                "Documentation Agent",
                "codex/sections/01-dragon-idea-engine-master-codex.md",
                null
            ),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Summarize the broader operator impact of the targeted implementation.",
                ["changedArtifactRollup"] = "docs/generated/provider-notes.md|docs/generated/provider-rollup.md"
            }
        );

        var request = AgentPromptFactory.Build(job);

        Assert.Contains("explain the broader documentation impact across the changed artifact rollup", request.Instructions!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildPrompt_StrengthensRefactorInstructions_ForTargetedFollowUpJobs()
    {
        var job = new SelfBuildJob(
            "refactor",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            430,
            new SelfBuildJobPayload(
                "[Story] Dragon Idea Engine Master Codex: Refactor Agent",
                ["story"],
                "Refactor Agent",
                "codex/sections/01-dragon-idea-engine-master-codex.md",
                null
            ),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["targetArtifact"] = "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                ["targetOutcome"] = "Improve prompt-construction clarity without changing behavior."
            }
        );

        var request = AgentPromptFactory.Build(job);

        Assert.Contains("treat that scoped artifact and outcome as the primary refactor surface", request.Instructions!, StringComparison.OrdinalIgnoreCase);
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
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider update."
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
        Assert.Equal("Produce an operator-facing summary of the provider update.", result.RequestedFollowUps[0].TargetOutcome);
        Assert.Contains("Operator summary", result.RequestedFollowUps[0].Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_CarriesStructuredFollowUpRequests_WithoutExplicitAgent_WhenArtifactImpliesOne()
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
                  "action": "summarize_issue",
                  "priority": "high",
                  "reason": "Operator summary should be generated immediately.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider update."
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);

        var result = executor.Execute(root, job);

        Assert.NotNull(result.RequestedFollowUps);
        Assert.Single(result.RequestedFollowUps!);
        Assert.True(string.IsNullOrWhiteSpace(result.RequestedFollowUps[0].Agent));
        Assert.Equal("docs/generated/provider-notes.md", result.RequestedFollowUps[0].TargetArtifact);
    }

    [Fact]
    public void Execute_CarriesStructuredFollowUpRequests_WithoutExplicitAction_WhenTargetImpliesOne()
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
                  "agent": "documentation",
                  "priority": "high",
                  "reason": "Operator summary should be generated immediately.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider update."
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);

        var result = executor.Execute(root, job);

        Assert.NotNull(result.RequestedFollowUps);
        Assert.Single(result.RequestedFollowUps!);
        Assert.True(string.IsNullOrWhiteSpace(result.RequestedFollowUps[0].Action));
        Assert.Equal("docs/generated/provider-notes.md", result.RequestedFollowUps[0].TargetArtifact);
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
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider notes."
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
        Assert.Equal("Produce an operator-facing summary of the provider notes.", feedbackFollowUp.Metadata["targetOutcome"]);
        Assert.Equal("documentation", feedbackFollowUp.Metadata["preferredAgent"]);
    }

    [Fact]
    public void CycleOnce_InfersAgentForRequestedFollowUps_WhenAgentIsOmitted()
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
                  "action": "summarize_issue",
                  "priority": "high",
                  "reason": "Summarize the documentation change for operators.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider notes."
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(436, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        var result = loop.CycleOnce(issues);

        var inferredFollowUp = Assert.Single(result.FollowUps, job => job.Agent == "documentation" && job.Action == "summarize_issue");
        Assert.Equal("documentation", inferredFollowUp.Metadata["preferredAgent"]);
    }

    [Fact]
    public void CycleOnce_InfersActionForRequestedFollowUps_WhenActionIsOmitted()
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
                  "agent": "documentation",
                  "priority": "high",
                  "reason": "Summarize the documentation change for operators.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider notes."
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(437, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        var result = loop.CycleOnce(issues);

        var inferredFollowUp = Assert.Single(result.FollowUps, job => job.Agent == "documentation" && job.Action == "summarize_issue");
        Assert.Equal("documentation", inferredFollowUp.Metadata["preferredAgent"]);
    }

    [Fact]
    public void CycleOnce_InfersImplementActionForRequestedFollowUps_WhenOutcomeImpliesUpdate()
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
                  "priority": "high",
                  "reason": "Tighten the provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Update the provider notes with clearer operator guidance."
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(438, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        var result = loop.CycleOnce(issues);

        var inferredFollowUp = Assert.Single(result.FollowUps, job => job.Agent == "documentation" && job.Action == "implement_issue");
        Assert.Equal("documentation", inferredFollowUp.Metadata["preferredAgent"]);
    }

    [Fact]
    public void CycleOnce_DropsEquivalentTargetedSummary_WhenTargetedImplementationExists()
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
                  "priority": "high",
                  "reason": "Summarize the updated provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider notes."
                },
                {
                  "priority": "high",
                  "reason": "Tighten the provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Update the provider notes with clearer operator guidance."
                }
              ]
            }
            """
        );
        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(442, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        var result = loop.CycleOnce(issues);

        Assert.Equal(3, result.FollowUps.Count);
        Assert.Contains(result.FollowUps, job => job.Agent == "review" && job.Action == "review_issue");
        Assert.Contains(result.FollowUps, job => job.Agent == "test" && job.Action == "test_issue");
        var implementationFollowUp = Assert.Single(result.FollowUps, job => job.Agent == "documentation" && job.Action == "implement_issue");
        Assert.Equal("docs/generated/provider-notes.md", implementationFollowUp.Metadata["deferredSummaryTargetArtifact"]);
        Assert.DoesNotContain(result.FollowUps, job => job.Agent == "documentation" && job.Action == "summarize_issue");
    }

    [Fact]
    public void CycleOnce_RequeuesDeferredTargetedSummary_AfterImplementationSucceeds()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "generated"));
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-notes.md"), "# Provider Notes\nInitial.\n");

        var provider = new SequencedFakeAgentModelProvider(
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
                  "priority": "high",
                  "reason": "Summarize the updated provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider notes."
                },
                {
                  "priority": "high",
                  "reason": "Tighten the provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Update the provider notes with clearer operator guidance."
                }
              ]
            }
            """,
            """
            {
              "summary": "Provider notes clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nClearer operator guidance.\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(443, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        var implementResult = loop.CycleOnce(issues);

        Assert.Contains(implementResult.FollowUps, job => job.Agent == "documentation" && job.Action == "summarize_issue");
        var summaryFollowUp = Assert.Single(implementResult.FollowUps, job => job.Agent == "documentation" && job.Action == "summarize_issue");
        Assert.Equal("docs/generated/provider-notes.md", summaryFollowUp.Metadata["targetArtifact"]);
        Assert.Equal("Produce an operator-facing summary of the provider notes.", summaryFollowUp.Metadata["targetOutcome"]);
    }

    [Fact]
    public void CycleOnce_DoesNotRequeueDeferredTargetedSummary_WhenImplementationDidNotChangeTargetArtifact()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "generated"));
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-notes.md"), "# Provider Notes\nInitial.\n");
        File.WriteAllText(Path.Combine(root, "docs", "generated", "other-notes.md"), "# Other Notes\nInitial.\n");

        var provider = new SequencedFakeAgentModelProvider(
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
                  "priority": "high",
                  "reason": "Summarize the updated provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider notes."
                },
                {
                  "priority": "high",
                  "reason": "Tighten the provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Update the provider notes with clearer operator guidance."
                }
              ]
            }
            """,
            """
            {
              "summary": "Other notes clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/other-notes.md",
                  "content": "# Other Notes\nUnrelated change.\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(444, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        var implementResult = loop.CycleOnce(issues);

        Assert.DoesNotContain(implementResult.FollowUps, job => job.Agent == "documentation" && job.Action == "summarize_issue");
    }

    [Fact]
    public void CycleOnce_DoesNotRequeueDeferredTargetedSummary_WhenSameArtifactImplementationIsAlreadyQueued()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "generated"));
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-notes.md"), "# Provider Notes\nInitial.\n");

        var provider = new SequencedFakeAgentModelProvider(
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
                  "priority": "high",
                  "reason": "Summarize the updated provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary of the provider notes."
                },
                {
                  "priority": "high",
                  "reason": "Tighten the provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Update the provider notes with clearer operator guidance."
                }
              ]
            }
            """,
            """
            {
              "summary": "Provider notes clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nClearer operator guidance.\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(448, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);

        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            1000,
            new SelfBuildJobPayload("[Story] Pending provider note update", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Apply a newer provider-notes improvement."
            }));

        var implementResult = loop.CycleOnce(issues);

        Assert.DoesNotContain(implementResult.FollowUps, job => job.Agent == "documentation" && job.Action == "summarize_issue");
    }

    [Fact]
    public void CycleOnce_RemovesQueuedSupersededSummaries_WhenSameArtifactImplementationIsQueued()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "generated"));
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-notes.md"), "# Provider Notes\nInitial.\n");

        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Provider notes clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nClearer operator guidance.\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var queue = new QueueStore(root);

        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            1001,
            new SelfBuildJobPayload("[Story] Stale provider summary", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "low",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Summarize the broader operator impact of the targeted implementation.",
                ["changedArtifactRollup"] = "docs/generated/provider-notes.md|docs/generated/provider-rollup.md"
            }));
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            1002,
            new SelfBuildJobPayload("[Story] Pending provider note update", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Apply a newer provider-notes improvement."
            }));

        var result = loop.CycleOnce([]);

        Assert.NotNull(result.Job);
        Assert.Equal(1002, result.Job!.Issue);
        Assert.Equal("implement_issue", result.Job.Action);
        Assert.Equal("1001", result.Job.Metadata["supersededSummaryIssues"]);
        Assert.Contains("summary", result.Job.Metadata["summaryConflictResolution"], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(loop.ReadQueue(), job => job.Issue == 1001 && job.Action == "summarize_issue");
    }

    [Fact]
    public void CycleOnce_RemovesLowerPriorityImplementation_WhenHigherPrioritySameArtifactImplementationIsQueued()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "generated"));
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-notes.md"), "# Provider Notes\nInitial.\n");

        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Provider notes clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nClearer operator guidance.\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var queue = new QueueStore(root);

        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            1003,
            new SelfBuildJobPayload("[Story] Older provider note update", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "low",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Apply an older provider-notes improvement."
            }));
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            1004,
            new SelfBuildJobPayload("[Story] Newer provider note update", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Apply a newer provider-notes improvement."
            }));

        var result = loop.CycleOnce([]);

        Assert.NotNull(result.Job);
        Assert.Equal(1004, result.Job!.Issue);
        Assert.Equal("implement_issue", result.Job.Action);
        Assert.DoesNotContain(loop.ReadQueue(), job => job.Issue == 1003 && job.Action == "implement_issue");
    }

    [Fact]
    public void CycleOnce_RemovesLessSpecificImplementation_WhenSamePrioritySameArtifactImplementationIsQueued()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "generated"));
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-notes.md"), "# Provider Notes\nInitial.\n");

        var provider = new FakeAgentModelProvider(
            """
            {
              "summary": "Provider notes clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nClearer operator guidance.\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var queue = new QueueStore(root);

        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            1005,
            new SelfBuildJobPayload("[Story] Generic provider note update", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Improve the provider notes."
            }));
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            1006,
            new SelfBuildJobPayload("[Story] Specific provider note update", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Update the provider notes with clearer operator guidance for queued follow-up execution."
            }));

        var result = loop.CycleOnce([]);

        Assert.NotNull(result.Job);
        Assert.Equal(1006, result.Job!.Issue);
        Assert.Equal("implement_issue", result.Job.Action);
        Assert.Equal("1005", result.Job.Metadata["supersededImplementationIssues"]);
        Assert.Contains("specificity", result.Job.Metadata["implementationConflictResolution"], StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.ExecutionRecord);
        Assert.Contains("Superseded implementation issues: 1005", result.ExecutionRecord!.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain(loop.ReadQueue(), job => job.Issue == 1005 && job.Action == "implement_issue");
    }

    [Fact]
    public void CycleOnce_EnqueuesOperatorSummary_WhenTargetedImplementationChangesMultipleArtifacts()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "generated"));
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-notes.md"), "# Provider Notes\nInitial.\n");
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-rollup.md"), "# Provider Rollup\nInitial.\n");

        var provider = new SequencedFakeAgentModelProvider(
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
                  "priority": "high",
                  "reason": "Tighten the provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Update the provider notes with clearer operator guidance."
                }
              ]
            }
            """,
            """
            {
              "summary": "Provider notes clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nClearer operator guidance.\n"
                },
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-rollup.md",
                  "content": "# Provider Rollup\nUpdated alongside notes.\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(445, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        var implementResult = loop.CycleOnce(issues);

        var documentationFollowUp = Assert.Single(implementResult.FollowUps, job => job.Agent == "documentation" && job.Action == "summarize_issue");
        Assert.Equal("docs/generated/provider-notes.md", documentationFollowUp.Metadata["targetArtifact"]);
        Assert.Equal("Summarize the broader operator impact of the targeted implementation.", documentationFollowUp.Metadata["targetOutcome"]);
        Assert.Equal("docs/generated/provider-notes.md|docs/generated/provider-rollup.md", documentationFollowUp.Metadata["changedArtifactRollup"]);
        Assert.Equal("low", documentationFollowUp.Metadata["requestedPriority"]);
    }

    [Fact]
    public void CycleOnce_EnqueuesFeedbackOperatorSummary_ForCodeTargetedMultiArtifactImplementation()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "backend", "src", "Dragon.Backend.Orchestrator"));
        Directory.CreateDirectory(Path.Combine(root, "backend", "tests", "Dragon.Backend.Tests"));
        File.WriteAllText(
            Path.Combine(root, "backend", "src", "Dragon.Backend.Orchestrator", "AgentPromptFactory.cs"),
            "namespace Dragon.Backend.Orchestrator;\npublic static class AgentPromptFactory { }\n");
        File.WriteAllText(
            Path.Combine(root, "backend", "tests", "Dragon.Backend.Tests", "PromptFactoryTests.cs"),
            "namespace Dragon.Backend.Tests;\npublic class PromptFactoryTests { }\n");

        var provider = new SequencedFakeAgentModelProvider(
            """
            {
              "summary": "Prompt factory reviewed.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                  "content": "namespace Dragon.Backend.Orchestrator;\npublic static class AgentPromptFactory { }\n"
                }
              ],
              "followUps": [
                {
                  "priority": "high",
                  "reason": "Tighten prompt construction.",
                  "targetArtifact": "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                  "targetOutcome": "Improve prompt construction clarity without changing behavior."
                }
              ]
            }
            """,
            """
            {
              "summary": "Prompt factory clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                  "content": "namespace Dragon.Backend.Orchestrator;\npublic static class AgentPromptFactory\n{\n}\n"
                },
                {
                  "type": "write_file",
                  "path": "backend/tests/Dragon.Backend.Tests/PromptFactoryTests.cs",
                  "content": "namespace Dragon.Backend.Tests;\npublic class PromptFactoryTests\n{\n}\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(446, "[Story] Dragon Idea Engine Master Codex: Refactor Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        var implementResult = loop.CycleOnce(issues);

        var feedbackFollowUp = Assert.Single(implementResult.FollowUps, job => job.Agent == "feedback" && job.Action == "summarize_issue");
        Assert.Equal("backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs", feedbackFollowUp.Metadata["targetArtifact"]);
        Assert.Equal("Summarize the broader operator impact of the targeted implementation.", feedbackFollowUp.Metadata["targetOutcome"]);
        Assert.Equal(
            "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs|backend/tests/Dragon.Backend.Tests/PromptFactoryTests.cs",
            feedbackFollowUp.Metadata["changedArtifactRollup"]);
        Assert.Equal("low", feedbackFollowUp.Metadata["requestedPriority"]);
    }

    [Fact]
    public void CycleOnce_SkipsBroadOperatorSummary_WhenSameArtifactImplementationIsAlreadyQueued()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "generated"));
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-notes.md"), "# Provider Notes\nInitial.\n");
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-rollup.md"), "# Provider Rollup\nInitial.\n");

        var provider = new SequencedFakeAgentModelProvider(
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
                  "priority": "high",
                  "reason": "Tighten the provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Update the provider notes with clearer operator guidance."
                }
              ]
            }
            """,
            """
            {
              "summary": "Provider notes clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nClearer operator guidance.\n"
                },
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-rollup.md",
                  "content": "# Provider Rollup\nUpdated alongside notes.\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(447, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);

        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            999,
            new SelfBuildJobPayload("[Story] Pending provider note update", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Apply a newer provider-notes improvement."
            }));

        var implementResult = loop.CycleOnce(issues);

        Assert.DoesNotContain(implementResult.FollowUps, job => job.Action == "summarize_issue" && job.Metadata.GetValueOrDefault("changedArtifactRollup") is not null);
    }

    [Fact]
    public void CycleOnce_ExecutesInferredDocumentationImplementFollowUp_AndAppliesOperations()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "docs", "generated"));
        File.WriteAllText(Path.Combine(root, "docs", "generated", "provider-notes.md"), "# Provider Notes\nInitial.\n");

        var provider = new SequencedFakeAgentModelProvider(
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
                  "priority": "high",
                  "reason": "Tighten the provider notes.",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Update the provider notes with clearer operator guidance."
                }
              ]
            }
            """,
            """
            {
              "summary": "Provider notes clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "docs/generated/provider-notes.md",
                  "content": "# Provider Notes\nClearer operator guidance.\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(439, "[Story] Dragon Idea Engine Master Codex: Documentation Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        var implementResult = loop.CycleOnce(issues);

        Assert.NotNull(implementResult.Job);
        Assert.Equal("documentation", implementResult.Job!.Agent);
        Assert.Equal("implement_issue", implementResult.Job.Action);
        Assert.NotNull(implementResult.Execution);
        Assert.Equal("success", implementResult.Execution!.Status);
        Assert.Equal(["docs/generated/provider-notes.md"], implementResult.Execution.ChangedPaths);
        Assert.Contains("Clearer operator guidance.", File.ReadAllText(Path.Combine(root, "docs", "generated", "provider-notes.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void CycleOnce_ExecutesInferredRefactorImplementFollowUp_AndAppliesOperations()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "backend", "src", "Dragon.Backend.Orchestrator"));
        File.WriteAllText(
            Path.Combine(root, "backend", "src", "Dragon.Backend.Orchestrator", "AgentPromptFactory.cs"),
            "namespace Dragon.Backend.Orchestrator;\npublic static class AgentPromptFactory { }\n");

        var provider = new SequencedFakeAgentModelProvider(
            """
            {
              "summary": "Prompt factory reviewed.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                  "content": "namespace Dragon.Backend.Orchestrator;\npublic static class AgentPromptFactory { }\n"
                }
              ],
              "followUps": [
                {
                  "priority": "high",
                  "reason": "Tighten prompt factory clarity.",
                  "targetArtifact": "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                  "targetOutcome": "Improve prompt construction clarity without changing behavior."
                }
              ]
            }
            """,
            """
            {
              "summary": "Prompt factory clarified.",
              "operations": [
                {
                  "type": "write_file",
                  "path": "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                  "content": "namespace Dragon.Backend.Orchestrator;\n// Clearer prompt-factory structure.\npublic static class AgentPromptFactory { }\n"
                }
              ]
            }
            """
        );

        var executor = new LocalJobExecutor((_, _, _) => new CommandResult(0, "ok", string.Empty), provider);
        var loop = new SelfBuildLoop(root, jobExecutor: executor);
        var issues = new[]
        {
            new GithubIssue(440, "[Story] Dragon Idea Engine Master Codex: Refactor Agent", "OPEN", ["story"])
        };

        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        loop.CycleOnce(issues);
        var implementResult = loop.CycleOnce(issues);

        Assert.NotNull(implementResult.Job);
        Assert.Equal("refactor", implementResult.Job!.Agent);
        Assert.Equal("implement_issue", implementResult.Job.Action);
        Assert.NotNull(implementResult.Execution);
        Assert.Equal("success", implementResult.Execution!.Status);
        Assert.Equal(["backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs"], implementResult.Execution.ChangedPaths);
        Assert.Contains("Clearer prompt-factory structure.", File.ReadAllText(Path.Combine(root, "backend", "src", "Dragon.Backend.Orchestrator", "AgentPromptFactory.cs")), StringComparison.Ordinal);
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
    public void QueueStore_PrefersMoreTargetedRequestedFollowUps_WhenPriorityIsEqual()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "architect",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            431,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Architect Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "normal",
                ["requestedReason"] = "Architecture summary is useful."
            }));
        queue.Enqueue(new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            431,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Feedback Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "normal",
                ["requestedReason"] = "Summarize the exact artifact change for operators.",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Produce an operator-facing summary of the provider notes."
            }));

        var next = queue.Dequeue();

        Assert.NotNull(next);
        Assert.Equal("feedback", next!.Agent);
        Assert.Equal("docs/generated/provider-notes.md", next.Metadata["targetArtifact"]);
        Assert.Equal("Produce an operator-facing summary of the provider notes.", next.Metadata["targetOutcome"]);
    }

    [Fact]
    public void QueueStore_PrefersRoleAlignedTargetedFollowUps_WhenTargetingIsEqual()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            433,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Feedback Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "normal",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Produce an operator-facing summary of the provider notes."
            }));
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            433,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Documentation Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "normal",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Produce an operator-facing summary of the provider notes."
            }));

        var next = queue.Dequeue();

        Assert.NotNull(next);
        Assert.Equal("documentation", next!.Agent);
    }

    [Fact]
    public void QueueStore_UsesArtifactPathToPreferRefactorForCodeTargets()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            434,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Documentation Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "normal",
                ["targetArtifact"] = "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                ["targetOutcome"] = "Improve prompt construction clarity without changing behavior."
            }));
        queue.Enqueue(new SelfBuildJob(
            "refactor",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            434,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Refactor Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "normal",
                ["targetArtifact"] = "backend/src/Dragon.Backend.Orchestrator/AgentPromptFactory.cs",
                ["targetOutcome"] = "Improve prompt construction clarity without changing behavior."
            }));

        var next = queue.Dequeue();

        Assert.NotNull(next);
        Assert.Equal("refactor", next!.Agent);
    }

    [Fact]
    public void QueueStore_PrefersImplementIssueOverSummaries_ForEquallyTargetedRequestedWork()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            441,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Documentation Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Produce an operator-facing summary of the provider notes.",
                ["preferredAgent"] = "documentation"
            }));
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "implement_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            441,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Documentation Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Update the provider notes with clearer operator guidance.",
                ["preferredAgent"] = "documentation"
            }));

        var next = queue.Dequeue();

        Assert.NotNull(next);
        Assert.Equal("implement_issue", next!.Action);
    }

    [Fact]
    public void QueueStore_UsesPreferredAgentMetadata_ForRoleAlignment()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "feedback",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            435,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Feedback Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "normal",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Produce an operator-facing summary of the provider notes.",
                ["preferredAgent"] = "documentation"
            }));
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            435,
            new SelfBuildJobPayload("[Story] Dragon Idea Engine Master Codex: Documentation Agent", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "normal",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Produce an operator-facing summary of the provider notes.",
                ["preferredAgent"] = "feedback"
            }));

        var next = queue.Dequeue();

        Assert.NotNull(next);
        Assert.Equal("feedback", next!.Agent);
        Assert.Equal("documentation", next.Metadata["preferredAgent"]);
    }

    [Fact]
    public void QueueStore_PrefersNarrowTargetedSummariesOverBroadRollupSummaries_WhenOtherRankingIsEqual()
    {
        var root = CreateTempRoot();
        var queue = new QueueStore(root);
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            501,
            new SelfBuildJobPayload("[Story] Broad summary", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Summarize the broader operator impact of the targeted implementation.",
                ["preferredAgent"] = "documentation",
                ["changedArtifactRollup"] = "docs/generated/provider-notes.md|docs/generated/provider-rollup.md"
            }));
        queue.Enqueue(new SelfBuildJob(
            "documentation",
            "summarize_issue",
            "IdeaEngine",
            "DragonIdeaEngine",
            502,
            new SelfBuildJobPayload("[Story] Narrow summary", ["story"], null, null, null),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["requestedPriority"] = "high",
                ["targetArtifact"] = "docs/generated/provider-notes.md",
                ["targetOutcome"] = "Produce an operator-facing summary of the provider notes.",
                ["preferredAgent"] = "documentation"
            }));

        var next = queue.Dequeue();

        Assert.NotNull(next);
        Assert.Equal(502, next!.Issue);
        Assert.Equal("summarize_issue", next.Action);
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
                  "targetArtifact": "docs/ARCHITECTURE.md",
                  "targetOutcome": "Produce a concise operator-facing architecture summary."
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
        Assert.Equal("Produce a concise operator-facing architecture summary.", parsed.FollowUps[0].TargetOutcome);
        Assert.Contains("Operators need", parsed.FollowUps[0].Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseStructuredResult_AllowsFollowUpWithoutExplicitAgent()
    {
        var parsed = AgentStructuredResultParser.Parse(
            """
            {
              "summary": "Documentation updated.",
              "followUps": [
                {
                  "action": "summarize_issue",
                  "priority": "high",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary."
                }
              ]
            }
            """
        );

        Assert.NotNull(parsed);
        Assert.NotNull(parsed!.FollowUps);
        Assert.Single(parsed.FollowUps!);
        Assert.True(string.IsNullOrWhiteSpace(parsed.FollowUps[0].Agent));
        Assert.Equal("summarize_issue", parsed.FollowUps[0].Action);
    }

    [Fact]
    public void ParseStructuredResult_AllowsFollowUpWithoutExplicitAction()
    {
        var parsed = AgentStructuredResultParser.Parse(
            """
            {
              "summary": "Documentation updated.",
              "followUps": [
                {
                  "agent": "documentation",
                  "priority": "high",
                  "targetArtifact": "docs/generated/provider-notes.md",
                  "targetOutcome": "Produce an operator-facing summary."
                }
              ]
            }
            """
        );

        Assert.NotNull(parsed);
        Assert.NotNull(parsed!.FollowUps);
        Assert.Single(parsed.FollowUps!);
        Assert.True(string.IsNullOrWhiteSpace(parsed.FollowUps[0].Action));
        Assert.Equal("documentation", parsed.FollowUps[0].Agent);
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

    private sealed class SequencedFakeAgentModelProvider : IAgentModelProvider
    {
        private readonly Queue<string> outputs;

        public SequencedFakeAgentModelProvider(params string[] outputs)
        {
            this.outputs = new Queue<string>(outputs);
        }

        public AgentModelProviderDescriptor Describe() =>
            new("fake", "memory", "gpt-5", "OPENAI_API_KEY", "test provider");

        public Task<AgentModelResponse> GenerateAsync(AgentModelRequest request, CancellationToken cancellationToken = default)
        {
            var output = outputs.Count > 0 ? outputs.Dequeue() : """{"summary":"No output configured."}""";
            return Task.FromResult(new AgentModelResponse("fake", request.Model, "resp_test", output, "completed"));
        }
    }
}
