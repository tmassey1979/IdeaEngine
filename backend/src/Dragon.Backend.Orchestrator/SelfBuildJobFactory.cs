using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static class SelfBuildJobFactory
{
    public static SelfBuildJob Create(GithubIssue issue, string agent, string repo, string project)
    {
        return CreateInternal(issue, agent, repo, project, null);
    }

    public static SelfBuildJob CreateRetry(GithubIssue issue, string agent, string repo, string project, IReadOnlyList<string>? changedPaths = null)
    {
        return CreateInternal(issue, agent, repo, project, changedPaths);
    }

    private static SelfBuildJob CreateInternal(GithubIssue issue, string agent, string repo, string project, IReadOnlyList<string>? changedPaths)
    {
        var isRecovery = issue.Labels.Contains("recovery", StringComparer.OrdinalIgnoreCase) ||
            issue.Title.Contains("[Recovery]", StringComparison.OrdinalIgnoreCase);
        IReadOnlyList<DeveloperOperation>? operations = null;

        if (SupportsPlannedOperations(agent))
        {
            operations = DeveloperOperationPlanner.Plan(issue);
        }
        var implementationProfile = DeveloperOperationPlanner.DescribeImplementationProfile(issue);

        var payload = new SelfBuildJobPayload(
            issue.Title,
            issue.Labels,
            issue.Heading,
            issue.SourceFile,
            operations
        );

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["requestedBy"] = "system",
            ["source"] = "dragon-orchestrator-dotnet",
            ["issueNumber"] = issue.Number.ToString(),
            ["workType"] = isRecovery ? "recovery" : "story"
        };
        if (issue.SourceIssueNumber is not null)
        {
            metadata["sourceIssueNumber"] = issue.SourceIssueNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (changedPaths?.Count > 0)
        {
            metadata["changedPaths"] = string.Join('|', changedPaths);
        }
        if (!string.IsNullOrWhiteSpace(implementationProfile))
        {
            metadata["implementationProfile"] = implementationProfile!;
        }

        return new SelfBuildJob(
            agent,
            ResolveAction(agent, isRecovery),
            repo,
            project,
            issue.Number,
            payload,
            metadata
        );
    }

    private static bool SupportsPlannedOperations(string agent) =>
        string.Equals(agent, "developer", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(agent, "documentation", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(agent, "repository-manager", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(agent, "refactor", StringComparison.OrdinalIgnoreCase);

    private static string ResolveAction(string agent, bool isRecovery)
    {
        if (isRecovery)
        {
            return "recover_issue";
        }

        return agent.ToLowerInvariant() switch
        {
            "review" => "review_issue",
            "test" => "test_issue",
            _ => "implement_issue"
        };
    }
}
