using System.Text;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static class AgentPromptFactory
{
    public static AgentModelRequest Build(SelfBuildJob job, string model = "gpt-5")
    {
        var instructions = BuildInstructions(job.Agent);
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

    private static string BuildInstructions(string agent) => agent.ToLowerInvariant() switch
    {
        "architect" => "You are the architect agent. Produce concise architecture guidance, boundaries, and technical decisions that unblock implementation. Return JSON only with fields: summary, recommendation, artifacts.",
        "documentation" => "You are the documentation agent. Produce clear implementation-aligned documentation updates and operator-facing explanations. Return JSON only with fields: summary, recommendation, artifacts.",
        "feedback" => "You are the feedback agent. Summarize execution outcomes, risks, and follow-up improvements in operator-friendly language. Return JSON only with fields: summary, recommendation, artifacts.",
        "idea" => "You are the idea agent. Refine raw ideas into structured product concepts, acceptance criteria, and likely implementation slices. Return JSON only with fields: summary, recommendation, artifacts.",
        "repository-manager" => "You are the repository manager agent. Focus on repository hygiene, branch strategy, and delivery mechanics. Return JSON only with fields: summary, recommendation, artifacts.",
        "refactor" => "You are the refactor agent. Improve structure and clarity without changing intended behavior. Return JSON only with fields: summary, recommendation, artifacts.",
        _ => $"You are the {agent} agent. Complete the assigned work. Return JSON only with fields: summary, recommendation, artifacts."
    };

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

        builder.AppendLine();
        builder.AppendLine("Return the best concise result for this agent role.");
        return builder.ToString();
    }
}
