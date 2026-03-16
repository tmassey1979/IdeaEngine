using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed class GithubIssueService
{
    private readonly GithubCommandRunner commandRunner;

    public GithubIssueService(GithubCommandRunner? commandRunner = null)
    {
        this.commandRunner = commandRunner ?? GithubCli.Run;
    }

    public IReadOnlyList<GithubIssue> ListStoryIssues(string owner, string repo, string rootDirectory)
    {
        var json = commandRunner(
            $"issue list --repo {owner}/{repo} --state open --limit 500 --json number,title,body,state,labels",
            rootDirectory
        );

        var backlogIndex = BacklogIndexLoader.Load(rootDirectory);
        using var document = JsonDocument.Parse(json);
        var issues = new List<GithubIssue>();

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var labels = entry.GetProperty("labels")
                .EnumerateArray()
                .Select(label => label.GetProperty("name").GetString())
                .OfType<string>()
                .ToArray();

            if (!labels.Contains("story", StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var title = entry.GetProperty("title").GetString() ?? string.Empty;
            backlogIndex.TryGetValue(title, out var metadata);

            issues.Add(new GithubIssue(
                entry.GetProperty("number").GetInt32(),
                title,
                entry.GetProperty("state").GetString() ?? "OPEN",
                labels,
                entry.GetProperty("body").GetString() ?? string.Empty,
                metadata?.Heading,
                metadata?.SourceFile
            ));
        }

        return issues.OrderBy(issue => issue.Number).ToArray();
    }

    public GithubSyncResult SyncWorkflow(string owner, string repo, IssueWorkflowState workflow, string rootDirectory)
    {
        if (!string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase))
        {
            return new GithubSyncResult(false, false, "Workflow is not validated yet.");
        }

        var commentBody = string.Join(
            Environment.NewLine,
            [
                "Automated backend sync update:",
                $"- workflow status: {workflow.OverallStatus}",
                $"- stages: {string.Join(", ", workflow.Stages.Select(stage => $"{stage.Key}={stage.Value.Status}"))}"
            ]
        );

        commandRunner(
            $"issue comment {workflow.IssueNumber} --repo {owner}/{repo} --body \"{EscapeForDoubleQuotes(commentBody)}\"",
            rootDirectory
        );
        commandRunner(
            $"issue close {workflow.IssueNumber} --repo {owner}/{repo} --comment \"{EscapeForDoubleQuotes("Closing issue from validated C# self-build workflow.")}\"",
            rootDirectory
        );

        return new GithubSyncResult(true, true, $"Updated GitHub issue #{workflow.IssueNumber}.");
    }

    private static string EscapeForDoubleQuotes(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
