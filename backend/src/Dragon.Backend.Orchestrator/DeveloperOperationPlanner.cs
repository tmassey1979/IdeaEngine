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
        var matcher = BuildStoryMatcher(issue);
        var heading = issue.Heading ?? string.Empty;
        var body = issue.Body ?? string.Empty;
        var rule = Rules.FirstOrDefault(candidate => candidate.Match.IsMatch($"{heading} {issue.Title}"));

        if (matcher.Matches("core system principles"))
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

        if (matcher.Matches("registry architecture", "capability catalog"))
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

        if (matcher.Matches("developer agent"))
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

        if (matcher.Matches("agent runner"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/runner/dragon-agent-runner/README.md",
                    RenderRunnerTemplateReadme(issue)),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/runner/dragon-agent-runner/package.json",
                    RenderRunnerPackageTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/runner/dragon-agent-runner/service-mode.json",
                    RenderRunnerServiceTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/runner/dragon-agent-runner/src/index.ts",
                    RenderRunnerEntryPointTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/runner/dragon-agent-runner/tsconfig.json",
                    RenderRunnerTsConfigTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/runner/dragon-agent-runner/tests/runner.test.ts",
                    RenderRunnerTestTemplate())
            ];
        }

        if (matcher.Matches("plugin system", "agent interface"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/agents/package.json",
                    RenderPluginPackageTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/agents/dragon-agent.ts",
                    RenderPluginTemplate(issue)),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/agents/dragon-agent.manifest.json",
                    RenderPluginManifestTemplate(issue)),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/agents/tsconfig.json",
                    RenderPluginTsConfigTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/agents/tests/dragon-agent.test.ts",
                    RenderPluginTestTemplate())
            ];
        }

        if (matcher.Matches("job message schema"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/contracts/job.schema.json",
                    RenderJobSchemaTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/contracts/job.example.json",
                    RenderJobExampleTemplate())
            ];
        }

        if (matcher.Matches("dragon agent sdk", "sdk package"))
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
                    RenderSdkTsConfigTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/tests/sdk-smoke.test.ts",
                    RenderSdkSmokeTestTemplate())
            ];
        }

        if (matcher.Matches("sdk responsibilities", "agent context", "agent result"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/src/index.ts",
                    RenderSdkIndexTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/src/types.ts",
                    RenderSdkTypesTemplate())
            ];
        }

        if (matcher.Matches("workspace utilities"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/src/workspace.ts",
                    RenderSdkWorkspaceTemplate())
            ];
        }

        if (matcher.Matches("credential manager"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/src/credentials.ts",
                    RenderSdkCredentialsTemplate())
            ];
        }

        if (matcher.Matches("job publishing"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/src/messaging.ts",
                    RenderSdkMessagingTemplate())
            ];
        }

        if (matcher.Matches("logging"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/dragon-agent-sdk/src/logging.ts",
                    RenderSdkLoggingTemplate())
            ];
        }

        if (matcher.Matches("example agent using sdk"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/examples/package.json",
                    RenderSdkExamplePackageTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/examples/developer-agent.ts",
                    RenderSdkExampleAgentTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/examples/developer-agent.manifest.json",
                    RenderSdkExampleAgentManifestTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/examples/tsconfig.json",
                    RenderSdkExampleTsConfigTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/sdk/examples/tests/developer-agent.test.ts",
                    RenderSdkExampleTestTemplate())
            ];
        }

        if (matcher.Matches("credentials system", "credentials"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/config/credentials.schema.json",
                    RenderCredentialsSchemaTemplate())
            ];
        }

        if (matcher.Matches("supported git providers", "git utilities", "github", "gitlab", "gitea", "clonerepo", "createpullrequest"))
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

        if (matcher.Matches("docker deployment", "containers on the pi"))
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

        if (matcher.Matches("api gateway component", "authentication and identity"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-api/Dragon.Api.csproj",
                    RenderDotnetApiProjectTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-api/Program.cs",
                    RenderDotnetApiProgramTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-api/appsettings.json",
                    RenderDotnetApiSettingsTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-api/appsettings.Development.json",
                    RenderDotnetApiDevelopmentSettingsTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-api/Properties/launchSettings.json",
                    RenderDotnetApiLaunchSettingsTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-api/Dockerfile",
                    RenderDotnetApiDockerfileTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-api/tests/Dragon.Api.Tests.csproj",
                    RenderDotnetApiTestsProjectTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-api/tests/HealthEndpointTests.cs",
                    RenderDotnetApiTestsTemplate())
            ];
        }

        if (matcher.Matches("database layer", "messaging layer", "standard service templates"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-worker/Dragon.Worker.csproj",
                    RenderDotnetWorkerProjectTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-worker/Program.cs",
                    RenderDotnetWorkerProgramTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-worker/WorkerOptions.cs",
                    RenderDotnetWorkerOptionsTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-worker/appsettings.json",
                    RenderDotnetWorkerSettingsTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-worker/appsettings.Development.json",
                    RenderDotnetWorkerDevelopmentSettingsTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-worker/Dockerfile",
                    RenderDotnetWorkerDockerfileTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-worker/tests/Dragon.Worker.Tests.csproj",
                    RenderDotnetWorkerTestsProjectTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/dotnet/dragon-worker/tests/WorkerOptionsTests.cs",
                    RenderDotnetWorkerTestsTemplate())
            ];
        }

        if (matcher.Matches("project initialization", "repository creation", "repository manager agent"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/pipeline/project-factory/package.json",
                    RenderProjectFactoryPackageTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/pipeline/project-factory/src/repository-manager.ts",
                    RenderRepositoryManagerTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/pipeline/project-factory/src/project-bootstrap.ts",
                    RenderProjectBootstrapTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/pipeline/project-factory/tsconfig.json",
                    RenderProjectFactoryTsConfigTemplate())
            ];
        }

        if (matcher.Matches("code generation", "task router", "workflow engine", "pipeline overview"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/pipeline/project-factory/src/task-router.ts",
                    RenderTaskRouterTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/pipeline/project-factory/src/workflow-engine.ts",
                    RenderWorkflowEngineTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/pipeline/project-factory/src/code-generator.ts",
                    RenderCodeGeneratorTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/pipeline/project-factory/tests/pipeline.test.ts",
                    RenderProjectFactoryTestTemplate())
            ];
        }

        if (matcher.Matches("execution monitor", "pipeline monitoring"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/observability/pipeline-monitoring.json",
                    RenderPipelineMonitoringTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/observability/pipeline-alert-rules.json",
                    RenderPipelineAlertRulesTemplate())
            ];
        }

        if (matcher.Matches("agent health monitoring", "agent performance monitoring"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/observability/agent-health-metrics.json",
                    RenderAgentMonitoringTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/observability/agent-health-alerts.json",
                    RenderAgentMonitoringAlertsTemplate())
            ];
        }

        if (matcher.Matches("monitoring and observability"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/observability/cluster-observability.json",
                    RenderClusterObservabilityTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/observability/cluster-dashboard.json",
                    RenderClusterDashboardTemplate())
            ];
        }

        if (matcher.Matches("continuous monitoring"))
        {
            return
            [
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/security/continuous-monitoring.json",
                    RenderContinuousMonitoringTemplate()),
                new DeveloperOperation(
                    "write_file",
                    "templates/repo-templates/security/continuous-monitoring-playbook.md",
                    RenderContinuousMonitoringPlaybookTemplate())
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

    private static string RenderPluginManifestTemplate(GithubIssue issue)
    {
        var agentName = InferPluginTemplateName(issue);
        var description = issue.Heading ?? issue.Title;
        return $$"""
{
  "name": "{{agentName}}",
  "displayName": "{{description}}",
  "entry": "./dragon-agent.ts",
  "runtime": "node",
  "capabilities": [
    "execute-job",
    "emit-artifacts"
  ]
}
""";
    }

    private static string RenderPluginPackageTemplate() =>
        """
{
  "name": "dragon-agent-plugin",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "build": "tsc -p tsconfig.json",
    "test": "node --input-type=module -e \"import('./dist/tests/dragon-agent.test.js')\""
  }
}
""";

    private static string RenderPluginTsConfigTemplate() =>
        """
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "moduleResolution": "Bundler",
    "outDir": "dist",
    "strict": true,
    "skipLibCheck": true
  },
  "include": ["*.ts", "tests/**/*.ts"]
}
""";

    private static string RenderPluginTestTemplate() =>
        """
import agent from "../dragon-agent";

export async function pluginSmokeTest(): Promise<boolean> {
  const result = await agent.execute({ mode: "cli" });
  return result.success;
}
""";

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

    private static string RenderJobExampleTemplate() =>
        """
{
  "jobId": "job-42",
  "agent": "developer",
  "action": "implement_issue",
  "repo": "IdeaEngine",
  "project": "DragonIdeaEngine",
  "issue": 42,
  "priority": "high",
  "createdAt": "2026-03-23T00:00:00Z",
  "payload": {
    "title": "[Story] Example Job",
    "labels": ["story"],
    "heading": "Example Job Message Schema"
  },
  "metadata": {
    "requestedBy": "system",
    "source": "dragon-orchestrator-dotnet"
  }
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
    "build": "tsc -p tsconfig.json",
    "test": "node --input-type=module -e \"import('./dist/tests/sdk-smoke.test.js')\""
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
  "include": ["src/**/*.ts", "tests/**/*.ts"]
}
""";

    private static string RenderSdkSmokeTestTemplate() =>
        """
import type { DragonAgentSdk } from "../src/index";

export function sdkSmokeTest(_sdk?: DragonAgentSdk): boolean {
  return true;
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

    private static string RenderSdkTypesTemplate() =>
        """
export interface DragonAgentContext {
  mode: "cli" | "service";
  payload?: unknown;
  metadata?: Record<string, string>;
}

export interface DragonAgentResult {
  success: boolean;
  message?: string;
  artifacts?: Record<string, unknown>;
  metrics?: Record<string, number>;
}
""";

    private static string RenderSdkWorkspaceTemplate() =>
        """
export interface DragonWorkspace {
  cloneRepo(repo: string): Promise<string>;
  prepareWorktree(path: string): Promise<void>;
}

export function createWorkspaceUtilities(): DragonWorkspace {
  return {
    async cloneRepo(repo) {
      return repo;
    },
    async prepareWorktree(_path) {}
  };
}
""";

    private static string RenderSdkCredentialsTemplate() =>
        """
export interface DragonCredentials {
  resolve(scope: string): Promise<Record<string, string>>;
}

export function createCredentialManager(): DragonCredentials {
  return {
    async resolve(_scope) {
      return {};
    }
  };
}
""";

    private static string RenderSdkMessagingTemplate() =>
        """
export interface DragonMessagePublisher {
  publish(queue: string, message: unknown): Promise<void>;
}

export function createMessagePublisher(): DragonMessagePublisher {
  return {
    async publish(_queue, _message) {}
  };
}
""";

    private static string RenderSdkLoggingTemplate() =>
        """
export interface DragonLogger {
  info(message: string, context?: Record<string, unknown>): void;
  error(message: string, context?: Record<string, unknown>): void;
}

export function createLogger(): DragonLogger {
  return {
    info(_message, _context) {},
    error(_message, _context) {}
  };
}
""";

    private static string RenderSdkExampleAgentTemplate() =>
        """
import type { DragonAgentContext, DragonAgentResult } from "../dragon-agent-sdk/src/types";

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

    private static string RenderSdkExampleAgentManifestTemplate() =>
        """
{
  "name": "developer",
  "entry": "./developer-agent.ts",
  "sdk": "dragon-agent-sdk",
  "capabilities": [
    "implement-issue",
    "emit-summary"
  ]
}
""";

    private static string RenderSdkExamplePackageTemplate() =>
        """
{
  "name": "dragon-agent-sdk-examples",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "build": "tsc -p tsconfig.json",
    "test": "node --input-type=module -e \"import('./dist/tests/developer-agent.test.js')\""
  }
}
""";

    private static string RenderSdkExampleTsConfigTemplate() =>
        """
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "moduleResolution": "Bundler",
    "outDir": "dist",
    "strict": true,
    "skipLibCheck": true
  },
  "include": ["*.ts", "tests/**/*.ts"]
}
""";

    private static string RenderSdkExampleTestTemplate() =>
        """
import { developerAgent } from "../developer-agent";

export async function exampleAgentSmokeTest(): Promise<boolean> {
  const result = await developerAgent.execute({ mode: "cli" });
  return result.success;
}
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

    private static string RenderRunnerPackageTemplate() =>
        """
{
  "name": "dragon-agent-runner",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "build": "tsc -p tsconfig.json",
    "test": "node --input-type=module -e \"import('./dist/tests/runner.test.js')\""
  }
}
""";

    private static string RenderRunnerEntryPointTemplate() =>
        """
export async function runAgentRunner(mode: "cli" | "service"): Promise<void> {
  if (mode === "service") {
    console.log("Starting dragon-agent-runner in service mode.");
    return;
  }

  console.log("Starting dragon-agent-runner in CLI mode.");
}
""";

    private static string RenderRunnerTsConfigTemplate() =>
        """
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "moduleResolution": "Bundler",
    "outDir": "dist",
    "strict": true,
    "skipLibCheck": true
  },
  "include": ["src/**/*.ts", "tests/**/*.ts"]
}
""";

    private static string RenderRunnerTestTemplate() =>
        """
import { runAgentRunner } from "../src/index";

export async function runnerSmokeTest(): Promise<void> {
  await runAgentRunner("cli");
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

    private static string RenderDotnetApiProjectTemplate() =>
        """
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""";

    private static string RenderDotnetApiProgramTemplate() =>
        """
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/identity", () => Results.Ok(new { provider = "dragon" }));

app.Run();

public partial class Program;
""";

    private static string RenderDotnetApiSettingsTemplate() =>
        """
{
  "AllowedHosts": "*",
  "Authentication": {
    "Provider": "dragon"
  }
}
""";

    private static string RenderDotnetApiDevelopmentSettingsTemplate() =>
        """
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information"
    }
  }
}
""";

    private static string RenderDotnetApiLaunchSettingsTemplate() =>
        """
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "Dragon.Api": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
""";

    private static string RenderDotnetApiDockerfileTemplate() =>
        """
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Dragon.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Dragon.Api.dll"]
""";

    private static string RenderDotnetApiTestsProjectTemplate() =>
        """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\\Dragon.Api.csproj" />
  </ItemGroup>
</Project>
""";

    private static string RenderDotnetApiTestsTemplate() =>
        """
using Microsoft.AspNetCore.Mvc.Testing;

namespace Dragon.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task GetHealth_ReturnsSuccess()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }
}
""";

    private static string RenderDotnetWorkerProjectTemplate() =>
        """
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
""";

    private static string RenderDotnetWorkerProgramTemplate() =>
        """
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.AddHostedService<QueueWorker>();

var host = builder.Build();
await host.RunAsync();

public sealed class QueueWorker : BackgroundService
{
    private readonly WorkerOptions options;

    public QueueWorker(IOptions<WorkerOptions> options)
    {
        this.options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds), stoppingToken);
        }
    }
}
""";

    private static string RenderDotnetWorkerOptionsTemplate() =>
        """
public sealed class WorkerOptions
{
    public int PollSeconds { get; init; } = 10;
    public string QueueName { get; init; } = "dragon.jobs";
}
""";

    private static string RenderDotnetWorkerSettingsTemplate() =>
        """
{
  "Worker": {
    "PollSeconds": 10,
    "QueueName": "dragon.jobs"
  }
}
""";

    private static string RenderDotnetWorkerDevelopmentSettingsTemplate() =>
        """
{
  "Worker": {
    "PollSeconds": 2,
    "QueueName": "dragon.jobs.dev"
  }
}
""";

    private static string RenderDotnetWorkerDockerfileTemplate() =>
        """
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish Dragon.Worker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Dragon.Worker.dll"]
""";

    private static string RenderDotnetWorkerTestsProjectTemplate() =>
        """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\\Dragon.Worker.csproj" />
  </ItemGroup>
</Project>
""";

    private static string RenderDotnetWorkerTestsTemplate() =>
        """
namespace Dragon.Worker.Tests;

public sealed class WorkerOptionsTests
{
    [Fact]
    public void Defaults_AreStable()
    {
        var options = new WorkerOptions();

        Assert.Equal(10, options.PollSeconds);
        Assert.Equal("dragon.jobs", options.QueueName);
    }
}
""";

    private static string RenderProjectFactoryPackageTemplate() =>
        """
{
  "name": "dragon-project-factory",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "scripts": {
    "start": "node dist/project-bootstrap.js",
    "build": "tsc -p tsconfig.json",
    "test": "node --input-type=module -e \"import('./dist/tests/pipeline.test.js')\""
  }
}
""";

    private static string RenderRepositoryManagerTemplate() =>
        """
export interface RepositoryPlan {
  name: string;
  visibility: "public" | "private";
  labels: string[];
}

export async function createRepository(plan: RepositoryPlan): Promise<RepositoryPlan> {
  return {
    ...plan,
    labels: [...plan.labels]
  };
}
""";

    private static string RenderProjectBootstrapTemplate() =>
        """
import { createRepository } from "./repository-manager";

export async function bootstrapProject(name: string): Promise<void> {
  await createRepository({
    name,
    visibility: "public",
    labels: ["dragon", "autogenerated"]
  });
}
""";

    private static string RenderTaskRouterTemplate() =>
        """
export interface RoutedTask {
  agent: string;
  action: string;
  issue: number;
}

export function routeTask(labels: string[]): RoutedTask {
  if (labels.includes("documentation")) {
    return { agent: "documentation", action: "implement_issue", issue: 0 };
  }

  return { agent: "developer", action: "implement_issue", issue: 0 };
}
""";

    private static string RenderWorkflowEngineTemplate() =>
        """
export type WorkflowStage = "architecture" | "implementation" | "review" | "test" | "documentation";

export function buildWorkflow(): WorkflowStage[] {
  return ["architecture", "implementation", "review", "test", "documentation"];
}
""";

    private static string RenderCodeGeneratorTemplate() =>
        """
export interface GeneratedArtifact {
  path: string;
  content: string;
}

export function generateProjectSlice(name: string): GeneratedArtifact[] {
  return [
    {
      path: `src/${name}.ts`,
      content: `export const ${name} = true;`
    }
  ];
}
""";

    private static string RenderProjectFactoryTsConfigTemplate() =>
        """
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ES2022",
    "moduleResolution": "Bundler",
    "outDir": "dist",
    "strict": true,
    "skipLibCheck": true
  },
  "include": ["src/**/*.ts", "tests/**/*.ts"]
}
""";

    private static string RenderProjectFactoryTestTemplate() =>
        """
import { buildWorkflow } from "../src/workflow-engine";
import { generateProjectSlice } from "../src/code-generator";

export function pipelineSmokeTest(): boolean {
  return buildWorkflow().length > 0 && generateProjectSlice("sample").length > 0;
}
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

    private static string RenderPipelineAlertRulesTemplate() =>
        """
{
  "name": "pipeline-alert-rules",
  "rules": [
    {
      "name": "pipeline-failure-spike",
      "severity": "high",
      "condition": "failure_events_total > 0"
    },
    {
      "name": "slow-generation-window",
      "severity": "medium",
      "condition": "generation_duration_seconds > 120"
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

    private static string RenderAgentMonitoringAlertsTemplate() =>
        """
{
  "name": "agent-health-alerts",
  "rules": [
    {
      "name": "missed-heartbeats",
      "severity": "high",
      "condition": "heartbeat_signals_total == 0"
    },
    {
      "name": "agent-error-rate-spike",
      "severity": "medium",
      "condition": "error_rate > 0.1"
    }
  ]
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

    private static string RenderClusterDashboardTemplate() =>
        """
{
  "name": "cluster-dashboard",
  "panels": [
    "cluster-health",
    "queue-depth",
    "node-availability",
    "agent-success-rate"
  ],
  "refreshSeconds": 30
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

    private static string RenderContinuousMonitoringPlaybookTemplate() =>
        """
# Continuous Monitoring Playbook

## Trigger Conditions

- new vulnerability discovery
- regulation changes
- security patch requirements
- technology deprecations

## Response

1. open or update the tracked remediation issue
2. trigger automated updates when the risk is understood
3. document the validation result and next retry window
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

    private static StoryMatcher BuildStoryMatcher(GithubIssue issue) => new(
        issue.Title,
        issue.Heading ?? string.Empty,
        issue.Body ?? string.Empty,
        issue.SourceFile ?? string.Empty,
        issue.TechnicalDetails ?? []);

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

    private sealed record StoryMatcher(string Title, string Heading, string Body, string SourceFile, IReadOnlyList<string> TechnicalDetails)
    {
        public bool Matches(params string[] terms) => terms.Any(Matches);

        private bool Matches(string term) =>
            Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            Heading.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            Body.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            SourceFile.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            TechnicalDetails.Any(detail => detail.Contains(term, StringComparison.OrdinalIgnoreCase));
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
