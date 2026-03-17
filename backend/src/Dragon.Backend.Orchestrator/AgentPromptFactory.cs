using System.Text;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static class AgentPromptFactory
{
    public static AgentModelRequest Build(SelfBuildJob job, string model = "gpt-5")
    {
        var instructions = BuildInstructions(job);
        var metadata = new Dictionary<string, string>(job.Metadata, StringComparer.Ordinal)
        {
            ["issueNumber"] = job.Issue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["repo"] = job.Repo,
            ["project"] = job.Project
        };

        return new AgentModelRequest(
            job.Agent,
            job.Action,
            model,
            instructions,
            [
                new AgentModelMessage("system", "You are working inside Dragon Idea Engine. Stay within the assigned agent role and provide deterministic, implementation-focused output."),
                new AgentModelMessage("user", BuildUserPrompt(job))
            ],
            metadata
        );
    }

    private static string BuildInstructions(SelfBuildJob job)
    {
        var baseInstruction = job.Agent.ToLowerInvariant() switch
        {
            "architect" => "You are the architect agent. Produce concise architecture guidance, boundaries, and technical decisions that unblock implementation. Return JSON only with fields: summary, recommendation, artifacts.",
            "documentation" => "You are the documentation agent. Produce clear implementation-aligned documentation updates and operator-facing explanations. Return JSON only with fields: summary, recommendation, artifacts.",
            "feedback" => "You are the feedback agent. Summarize execution outcomes, risks, and follow-up improvements in operator-friendly language. Return JSON only with fields: summary, recommendation, artifacts.",
            "idea" => "You are the idea agent. Refine raw ideas into structured product concepts, acceptance criteria, and likely implementation slices. Return JSON only with fields: summary, recommendation, artifacts.",
            "repository-manager" => "You are the repository manager agent. Focus on repository hygiene, branch strategy, and delivery mechanics. Return JSON only with fields: summary, recommendation, artifacts.",
            "refactor" => "You are the refactor agent. Improve structure and clarity without changing intended behavior. Return JSON only with fields: summary, recommendation, artifacts.",
            _ => $"You are the {job.Agent} agent. Complete the assigned work. Return JSON only with fields: summary, recommendation, artifacts."
        };
        var hasTargeting = HasMetadataValue(job, "targetArtifact") || HasMetadataValue(job, "targetOutcome");
        var hasBroadRollup = HasBroadChangedArtifactRollup(job);

        if (string.Equals(job.Agent, "feedback", StringComparison.OrdinalIgnoreCase) && hasTargeting)
        {
            var instruction = $"{baseInstruction} When a target artifact or target outcome is provided, center the summary on that scoped work before broader observations.";
            if (hasBroadRollup)
            {
                instruction += " When a changed artifact rollup is present, explain the broader impact across the changed artifact rollup without losing the primary target focus.";
            }

            return instruction;
        }

        if (string.Equals(job.Agent, "documentation", StringComparison.OrdinalIgnoreCase) && hasTargeting)
        {
            var instruction = $"{baseInstruction} When a target artifact or target outcome is provided, treat that scoped artifact and outcome as the primary documentation surface before broader updates.";
            if (hasBroadRollup)
            {
                instruction += " When a changed artifact rollup is present, explain the broader documentation impact across the changed artifact rollup while keeping the primary target artifact central.";
            }

            return instruction;
        }

        if (string.Equals(job.Agent, "refactor", StringComparison.OrdinalIgnoreCase) && hasTargeting)
        {
            return $"{baseInstruction} When a target artifact or target outcome is provided, treat that scoped artifact and outcome as the primary refactor surface before broader cleanups.";
        }

        return baseInstruction;
    }

    private static bool HasMetadataValue(SelfBuildJob job, string key) =>
        job.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);

    private static bool HasBroadChangedArtifactRollup(SelfBuildJob job) =>
        job.Metadata.TryGetValue("changedArtifactRollup", out var value) &&
        !string.IsNullOrWhiteSpace(value) &&
        value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length > 1;

    private static string BuildUserPrompt(SelfBuildJob job)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Issue #{job.Issue}: {job.Payload.Title}");
        builder.AppendLine($"Agent: {job.Agent}");
        builder.AppendLine($"Action: {job.Action}");
        builder.AppendLine($"Project: {job.Project}");
        builder.AppendLine($"Repository: {job.Repo}");

        if (!string.IsNullOrWhiteSpace(job.Payload.Heading))
        {
            builder.AppendLine($"Heading: {job.Payload.Heading}");
        }

        if (!string.IsNullOrWhiteSpace(job.Payload.SourceFile))
        {
            builder.AppendLine($"Source file: {job.Payload.SourceFile}");
        }

        if (job.Payload.Labels.Count > 0)
        {
            builder.AppendLine($"Labels: {string.Join(", ", job.Payload.Labels)}");
        }

        if (job.Payload.Operations?.Count > 0)
        {
            builder.AppendLine("Planned developer operations:");
            foreach (var operation in job.Payload.Operations)
            {
                builder.AppendLine($"- {operation.Type}: {operation.Path}");
            }
        }

        if (job.Metadata.TryGetValue("targetArtifact", out var targetArtifact) && !string.IsNullOrWhiteSpace(targetArtifact))
        {
            builder.AppendLine($"Target artifact: {targetArtifact}");
        }

        if (job.Metadata.TryGetValue("requestedReason", out var requestedReason) && !string.IsNullOrWhiteSpace(requestedReason))
        {
            builder.AppendLine($"Requested reason: {requestedReason}");
        }

        if (job.Metadata.TryGetValue("targetOutcome", out var targetOutcome) && !string.IsNullOrWhiteSpace(targetOutcome))
        {
            builder.AppendLine($"Target outcome: {targetOutcome}");
        }

        if (job.Metadata.TryGetValue("changedArtifactRollup", out var changedArtifactRollup) && !string.IsNullOrWhiteSpace(changedArtifactRollup))
        {
            var rollup = string.Join(", ", changedArtifactRollup.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            builder.AppendLine($"Changed artifact rollup: {rollup}");
        }

        builder.AppendLine();
        builder.AppendLine("Return the best concise result for this agent role.");
        return builder.ToString();
    }
}
