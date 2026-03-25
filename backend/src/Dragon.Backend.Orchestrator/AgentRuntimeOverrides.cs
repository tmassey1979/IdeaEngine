namespace Dragon.Backend.Orchestrator;

public sealed record AgentRuntimeOverrides(
    string? ProviderName = null,
    string? ApiKey = null,
    string? Model = null,
    string? Endpoint = null,
    string? ConnectionString = null,
    string? EncryptionKey = null);
