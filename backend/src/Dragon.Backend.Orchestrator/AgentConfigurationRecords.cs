namespace Dragon.Backend.Orchestrator;

public sealed record StoredProviderConfiguration(
    string Name,
    string Transport,
    string DefaultModel,
    string Endpoint,
    string EncryptedApiKey,
    DateTimeOffset UpdatedAt);

public sealed record StoredAgentConfiguration(
    string AgentName,
    string? ProviderName,
    string? Model,
    bool Enabled,
    DateTimeOffset UpdatedAt);

public sealed record ResolvedAgentConfiguration(
    string ProviderName,
    string ApiKey,
    string Model,
    string Endpoint,
    string Source,
    string? AgentName);
