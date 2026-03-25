namespace Dragon.Backend.Orchestrator;

public interface IAgentConfigurationStore
{
    StoredProviderConfiguration? GetProvider(string providerName);

    IReadOnlyList<StoredProviderConfiguration> ListProviders();

    void UpsertProvider(StoredProviderConfiguration configuration);

    StoredAgentConfiguration? GetAgent(string agentName);

    IReadOnlyList<StoredAgentConfiguration> ListAgents();

    void UpsertAgent(StoredAgentConfiguration configuration);
}
