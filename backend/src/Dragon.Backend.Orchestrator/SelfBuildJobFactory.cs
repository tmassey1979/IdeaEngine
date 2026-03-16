using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static class SelfBuildJobFactory
{
    public static SelfBuildJob Create(GithubIssue issue, string agent, string repo, string project)
    {
        IReadOnlyList<DeveloperOperation>? operations = null;

        if (string.Equals(agent, "developer", StringComparison.OrdinalIgnoreCase))
        {
            operations = DeveloperOperationPlanner.Plan(issue);
        }

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
            ["issueNumber"] = issue.Number.ToString()
        };

        return new SelfBuildJob(
            agent,
            "implement_issue",
            repo,
            project,
            issue.Number,
            payload,
            metadata
        );
    }
}
