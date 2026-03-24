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
        ),
        new(
            "repository-structure-doc",
            RepositoryStructurePattern(),
            "docs/generated/repository-structure-notes.md",
            "Repository Structure"
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

        if (title.Contains("agent runner", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/runner/dragon-agent-runner/README.md",
                    RenderRunnerTemplateReadme(issue))
            ];
        }

        if (title.Contains("plugin system", StringComparison.Ordinal) ||
            title.Contains("agent interface", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/agents/dragon-agent.ts",
                    RenderPluginTemplate(issue))
            ];
        }

        if (title.Contains("job message schema", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/contracts/job.schema.json",
                    RenderJobSchemaTemplate())
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

    private static string RenderRunnerTemplateReadme(GithubIssue issue)
    {
        var builder = new StringBuilder();
        var excerpt = BuildExcerpt(issue.Body, 10, "Runner responsibilities are derived from backlog context.");

        builder.AppendLine("# dragon-agent-runner");
        builder.AppendLine();
        builder.AppendLine("Bootstrap template for the Dragon agent runner process.");
        builder.AppendLine();
        builder.AppendLine("## Responsibilities");
        builder.AppendLine();
        builder.AppendLine("- load agent plugins");
        builder.AppendLine("- run CLI commands");
        builder.AppendLine("- connect to RabbitMQ");
        builder.AppendLine("- execute queued jobs");
        builder.AppendLine("- return results");
        builder.AppendLine();
        builder.AppendLine("## CLI");
        builder.AppendLine();
        builder.AppendLine("```bash");
        builder.AppendLine("dragon-agent-runner developer --repo crm --issue 42");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## Service Mode");
        builder.AppendLine();
        builder.AppendLine("```bash");
        builder.AppendLine("dragon-agent-runner --service");
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## Source Context");
        builder.AppendLine();
        builder.AppendLine(excerpt);

        return builder.ToString();
    }

    private static string RenderPluginTemplate(GithubIssue issue)
    {
        var agentName = InferPluginTemplateName(issue);
        var description = issue.Heading ?? issue.Title;
        return $$"""
export interface DragonAgentContext {
  mode: "cli" | "service";
  payload?: unknown;
}

export interface DragonAgentResult {
  success: boolean;
  message?: string;
  artifacts?: Record<string, unknown>;
  metrics?: Record<string, number>;
}

export interface DragonAgent {
  name: string;
  description: string;
  registerArgs?(cli: unknown): void;
  execute(context: DragonAgentContext): Promise<DragonAgentResult>;
}

const {{agentName}}Agent: DragonAgent = {
  name: "{{agentName}}",
  description: "{{description}}",
  async execute(_context) {
    return {
      success: true,
      message: "Bootstrap plugin skeleton completed."
    };
  }
};

export default {{agentName}}Agent;
""";
    }

    private static string RenderJobSchemaTemplate() =>
        """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "DragonJob",
  "type": "object",
  "required": ["jobId", "agent", "action", "repo", "project", "issue", "priority", "createdAt", "payload", "metadata"],
  "properties": {
    "jobId": { "type": "string", "minLength": 1 },
    "agent": { "type": "string", "minLength": 1 },
    "action": { "type": "string", "minLength": 1 },
    "repo": { "type": "string", "minLength": 1 },
    "project": { "type": "string", "minLength": 1 },
    "issue": { "type": "integer", "minimum": 1 },
    "priority": {
      "type": "string",
      "enum": ["low", "normal", "high"]
    },
    "createdAt": {
      "type": "string",
      "format": "date-time"
    },
    "payload": {
      "type": "object"
    },
    "metadata": {
      "type": "object",
      "required": ["requestedBy", "source"],
      "properties": {
        "requestedBy": { "type": "string" },
        "source": { "type": "string" }
      },
      "additionalProperties": {
        "type": ["string", "number", "boolean", "null"]
      }
    }
  },
  "additionalProperties": true
}
""";

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

    private static string InferPluginTemplateName(GithubIssue issue)
    {
        var source = issue.Heading ?? issue.Title;
        var lowered = source.ToLowerInvariant();
        if (lowered.Contains("architect", StringComparison.Ordinal))
        {
            return "architect";
        }

        if (lowered.Contains("developer", StringComparison.Ordinal))
        {
            return "developer";
        }

        if (lowered.Contains("review", StringComparison.Ordinal))
        {
            return "review";
        }

        return "agent";
    }

    [GeneratedRegex("(architecture|core system principles|system architecture|registry architecture)", RegexOptions.IgnoreCase)]
    private static partial Regex ArchitecturePattern();

    [GeneratedRegex("(sdk|agent interface|agent context|agent result|developer agent)", RegexOptions.IgnoreCase)]
    private static partial Regex SdkPattern();

    [GeneratedRegex("(pipeline|workflow|loop|review|test|compliance|security|validation)", RegexOptions.IgnoreCase)]
    private static partial Regex OperationsPattern();

    [GeneratedRegex("(registry|capability|discovery|node|cluster|health monitoring)", RegexOptions.IgnoreCase)]
    private static partial Regex RegistryPattern();

    [GeneratedRegex("(repository structure|root repo|workspace structure|multi-repo)", RegexOptions.IgnoreCase)]
    private static partial Regex RepositoryStructurePattern();
}
