using System.Text;
using System.Text.RegularExpressions;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static partial class DeveloperOperationPlanner
{
    private sealed record PlannerRule(string Name, Regex Match, string TargetPath, string SectionTitle);

    private static readonly PlannerRule[] Rules =
    [
        new(
            "architecture-doc",
            ArchitecturePattern(),
            "docs/generated/architecture-notes.md",
            "Architecture Notes"
        ),
        new(
            "sdk-doc",
            SdkPattern(),
            "docs/generated/sdk-notes.md",
            "SDK Notes"
        ),
        new(
            "operations-doc",
            OperationsPattern(),
            "docs/generated/operations-notes.md",
            "Operations Notes"
        ),
        new(
            "registry-doc",
            RegistryPattern(),
            "docs/generated/agent-registry.md",
            "Agent Registry"
        )
    ];

    public static IReadOnlyList<DeveloperOperation> Plan(GithubIssue issue)
    {
        var title = issue.Title.ToLowerInvariant();
        var heading = issue.Heading ?? string.Empty;
        var body = issue.Body ?? string.Empty;
        var rule = Rules.FirstOrDefault(candidate => candidate.Match.IsMatch($"{heading} {issue.Title}"));

        if (title.Contains("core system principles", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "append_text",
                    "docs/ARCHITECTURE.md",
                    """

                    ## Core System Principles

                    - Agents are plugins loaded dynamically by the runner.
                    - The runner supports CLI and service execution modes.
                    - Jobs flow through an event-driven queue contract.
                    """
                )
            ];
        }

        if (title.Contains("registry architecture", StringComparison.Ordinal) || title.Contains("capability catalog", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "docs/AGENT_REGISTRY.md",
                    """
                    # Agent Registry

                    This document captures the current runtime capability catalog and the direction for registry-driven routing.

                    ## Current Capabilities

                    - architect
                    - developer
                    - review
                    - test
                    - refactor
                    """
                )
            ];
        }

        if (title.Contains("developer agent", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "append_text",
                    "docs/SDK.md",
                    """

                    ## Developer Operations

                    The developer agent supports bounded `write_file`, `append_text`, and `replace_text` operations for deterministic self-improvement tasks.
                    """
                )
            ];
        }

        if (rule is not null)
        {
            return
            [
                new DeveloperOperation(
                    "append_text",
                    rule.TargetPath,
                    RenderPlannedSection(rule.SectionTitle, issue)
                )
            ];
        }

        return
        [
            new DeveloperOperation(
                "append_text",
                "docs/BACKLOG_EXECUTION.md",
                RenderFallbackSection(issue, body)
            )
        ];
    }

    private static string RenderPlannedSection(string sectionTitle, GithubIssue issue)
    {
        var excerpt = BuildExcerpt(issue.Body, 8, "Planned automatically from backlog context.");
        var builder = new StringBuilder();

        builder.AppendLine();
        builder.AppendLine($"## {issue.Title}");
        builder.AppendLine();
        builder.AppendLine($"Planner category: {sectionTitle}");
        builder.AppendLine($"Source heading: {issue.Heading ?? "n/a"}");
        builder.AppendLine($"Source file: {issue.SourceFile ?? "n/a"}");
        builder.AppendLine();
        builder.AppendLine(excerpt);
        builder.AppendLine();

        return builder.ToString();
    }

    private static string RenderFallbackSection(GithubIssue issue, string body)
    {
        var excerpt = BuildExcerpt(body, 6, "Planned by the orchestrator for incremental self-build work.");
        var builder = new StringBuilder();

        builder.AppendLine();
        builder.AppendLine($"## Issue #{issue.Number}: {issue.Title}");
        builder.AppendLine();
        builder.AppendLine(excerpt);

        return builder.ToString();
    }

    private static string BuildExcerpt(string? body, int maxLines, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return fallback;
        }

        var lines = body
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(maxLines);

        return string.Join(Environment.NewLine, lines);
    }

    [GeneratedRegex("(architecture|core system principles|system architecture|registry architecture)", RegexOptions.IgnoreCase)]
    private static partial Regex ArchitecturePattern();

    [GeneratedRegex("(sdk|agent interface|agent context|agent result|developer agent)", RegexOptions.IgnoreCase)]
    private static partial Regex SdkPattern();

    [GeneratedRegex("(pipeline|workflow|loop|review|test|compliance|security|validation)", RegexOptions.IgnoreCase)]
    private static partial Regex OperationsPattern();

    [GeneratedRegex("(registry|capability|discovery|node|cluster|health monitoring)", RegexOptions.IgnoreCase)]
    private static partial Regex RegistryPattern();
}
