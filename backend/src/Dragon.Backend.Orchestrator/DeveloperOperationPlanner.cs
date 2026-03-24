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
                    RenderRunnerTemplateReadme(issue)),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/runner/dragon-agent-runner/service-mode.json",
                    RenderRunnerServiceTemplate())
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

        if (title.Contains("dragon agent sdk", StringComparison.Ordinal) ||
            title.Contains("sdk package", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/package.json",
                    RenderSdkPackageTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/tsconfig.json",
                    RenderSdkTsConfigTemplate())
            ];
        }

        if (title.Contains("sdk responsibilities", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/src/index.ts",
                    RenderSdkIndexTemplate())
            ];
        }

        if (title.Contains("example agent using sdk", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/examples/developer-agent.ts",
                    RenderSdkExampleAgentTemplate())
            ];
        }

        if (title.Contains("credentials system", StringComparison.Ordinal) ||
            title.Contains("credentials", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/config/credentials.schema.json",
                    RenderCredentialsSchemaTemplate())
            ];
        }

        if (title.Contains("supported git providers", StringComparison.Ordinal) ||
            title.Contains("git utilities", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/config/git-providers.json",
                    RenderGitProvidersTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/src/git.ts",
                    RenderGitUtilitiesTemplate())
            ];
        }

        if (title.Contains("docker deployment", StringComparison.Ordinal) ||
            title.Contains("containers on the pi", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/deploy/docker-compose.yml",
                    RenderDeploymentComposeTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/deploy/.env.example",
                    RenderDeploymentEnvTemplate())
            ];
        }

        if (title.Contains("execution monitor", StringComparison.Ordinal) ||
            title.Contains("pipeline monitoring", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/observability/pipeline-monitoring.json",
                    RenderPipelineMonitoringTemplate())
            ];
        }

        if (title.Contains("agent health monitoring", StringComparison.Ordinal) ||
            title.Contains("agent performance monitoring", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/observability/agent-health-metrics.json",
                    RenderAgentMonitoringTemplate())
            ];
        }

        if (title.Contains("monitoring and observability", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/observability/cluster-observability.json",
                    RenderClusterObservabilityTemplate())
            ];
        }

        if (title.Contains("continuous monitoring", StringComparison.Ordinal))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/security/continuous-monitoring.json",
                    RenderContinuousMonitoringTemplate())
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

    private static string RenderSdkPackageTemplate() =>
        """
{
  "name": "dragon-agent-sdk",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "main": "dist/index.js",
  "types": "dist/index.d.ts",
  "scripts": {
    "build": "tsc -p tsconfig.json"
  }
}
""";

    private static string RenderSdkTsConfigTemplate() =>
        """
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "moduleResolution": "Bundler",
    "declaration": true,
    "outDir": "dist",
    "strict": true,
    "skipLibCheck": true
  },
  "include": ["src/**/*.ts"]
}
""";

    private static string RenderSdkIndexTemplate() =>
        """
export interface DragonJobContext {
  job: unknown;
  payload: unknown;
  metadata: Record<string, string>;
}

export interface DragonWorkspace {
  cloneRepo(repo: string): Promise<string>;
}

export interface DragonGit {
  createBranch(name: string): Promise<void>;
  commit(message: string): Promise<void>;
  push(): Promise<void>;
}

export interface DragonCredentials {
  resolve(scope: string): Promise<Record<string, string>>;
}

export interface DragonAgentSdk {
  parseJob(input: string): DragonJobContext;
  publishMessage(queue: string, message: unknown): Promise<void>;
  workspace: DragonWorkspace;
  git: DragonGit;
  credentials: DragonCredentials;
}
""";

    private static string RenderSdkExampleAgentTemplate() =>
        """
import type { DragonAgentContext, DragonAgentResult } from "../dragon-agent-sdk/src/index";

export const developerAgent = {
  name: "developer",
  description: "implements repository issues",
  version: "1.0",
  async execute(context: DragonAgentContext): Promise<DragonAgentResult> {
    void context;

    return {
      success: true,
      message: "Bootstrap developer agent completed."
    };
  }
};
""";

    private static string RenderCredentialsSchemaTemplate() =>
        """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "DragonCredentials",
  "type": "object",
  "properties": {
    "system": {
      "$ref": "#/$defs/credentialSet"
    },
    "project": {
      "type": "object",
      "additionalProperties": {
        "$ref": "#/$defs/credentialSet"
      }
    }
  },
  "$defs": {
    "credentialSet": {
      "type": "object",
      "additionalProperties": {
        "type": "string"
      }
    }
  },
  "additionalProperties": false
}
""";

    private static string RenderGitProvidersTemplate() =>
        """
{
  "defaultProvider": "github",
  "providers": [
    {
      "name": "github",
      "enabled": true
    },
    {
      "name": "gitlab",
      "enabled": true
    },
    {
      "name": "gitea",
      "enabled": true
    },
    {
      "name": "bitbucket",
      "enabled": false
    }
  ]
}
""";

    private static string RenderGitUtilitiesTemplate() =>
        """
export interface DragonGit {
  cloneRepo(repo: string): Promise<string>;
  createBranch(name: string): Promise<void>;
  commit(message: string): Promise<void>;
  push(): Promise<void>;
  createPullRequest(title: string, body?: string): Promise<void>;
}

export function createGitUtilities(): DragonGit {
  return {
    async cloneRepo(repo) {
      return repo;
    },
    async createBranch(_name) {},
    async commit(_message) {},
    async push() {},
    async createPullRequest(_title, _body) {}
  };
}
""";

    private static string RenderRunnerServiceTemplate() =>
        """
{
  "mode": "service",
  "queue": "rabbitmq",
  "worker": "dragon-agent-runner",
  "polling": {
    "enabled": true
  }
}
""";

    private static string RenderDeploymentComposeTemplate() =>
        """
version: "3.9"

services:
  dragon-ui:
    image: dragon-ui:latest
  dragon-api:
    image: dragon-api:latest
  dragon-orchestrator:
    image: dragon-orchestrator:latest
  dragon-agent-runner:
    image: dragon-agent-runner:latest
  rabbitmq:
    image: rabbitmq:3-management
  postgres:
    image: postgres:16
""";

    private static string RenderDeploymentEnvTemplate() =>
        """
OPENAI_API_KEY=
GITHUB_TOKEN=
POSTGRES_PASSWORD=dragon
RABBITMQ_DEFAULT_USER=dragon
RABBITMQ_DEFAULT_PASS=dragon
""";

    private static string RenderPipelineMonitoringTemplate() =>
        """
{
  "name": "pipeline-monitoring",
  "metrics": [
    "generation_duration_seconds",
    "task_success_rate",
    "agent_utilization_percent",
    "failure_events_total"
  ],
  "dashboards": [
    "pipeline-overview",
    "pipeline-failures"
  ],
  "alerts": [
    {
      "name": "pipeline-failure-spike",
      "condition": "failure_events_total > 0"
    }
  ]
}
""";

    private static string RenderAgentMonitoringTemplate() =>
        """
{
  "name": "agent-health-monitoring",
  "metrics": [
    "heartbeat_signals_total",
    "task_completion_success_rate",
    "execution_duration_seconds",
    "error_rate",
    "quality_score",
    "resource_consumption_percent"
  ],
  "offlinePolicy": {
    "markOfflineAfterMissedHeartbeats": 3,
    "reassignTasks": true
  }
}
""";

    private static string RenderClusterObservabilityTemplate() =>
        """
{
  "name": "cluster-observability",
  "metrics": [
    "cpu_usage_percent",
    "memory_usage_percent",
    "task_queue_depth",
    "agent_success_rate",
    "node_availability"
  ],
  "visualizations": [
    "cluster-health",
    "queue-depth",
    "node-availability"
  ]
}
""";

    private static string RenderContinuousMonitoringTemplate() =>
        """
{
  "name": "continuous-compliance-monitoring",
  "checks": [
    "new_vulnerability_discovery",
    "regulation_changes",
    "security_patch_requirements",
    "technology_deprecations"
  ],
  "actions": {
    "openIssueOnFailure": true,
    "triggerAutomatedUpdates": true
  }
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
