using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public interface IAgentModelProvider
{
    AgentModelProviderDescriptor Describe();

    Task<AgentModelResponse> GenerateAsync(AgentModelRequest request, CancellationToken cancellationToken = default);
}
