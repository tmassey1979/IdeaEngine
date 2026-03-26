namespace Dragon.Backend.Orchestrator;

public sealed record AgentRuntimeOverrides(
    string? Model = null,
    string? ConnectionString = null,
    string? EncryptionKey = null);
