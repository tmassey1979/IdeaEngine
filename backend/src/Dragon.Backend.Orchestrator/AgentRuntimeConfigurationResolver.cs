namespace Dragon.Backend.Orchestrator;

public sealed class AgentRuntimeConfigurationResolver
{
    private readonly Func<string, string?> environmentReader;
    private readonly IAgentConfigurationStore? store;
    private readonly ConfigurationEncryptionService? encryptionService;
    private readonly AgentRuntimeOverrides? overrides;

    public AgentRuntimeConfigurationResolver(
        Func<string, string?> environmentReader,
        IAgentConfigurationStore? store = null,
        ConfigurationEncryptionService? encryptionService = null,
        AgentRuntimeOverrides? overrides = null)
    {
        this.environmentReader = environmentReader;
        this.store = store;
        this.encryptionService = encryptionService;
        this.overrides = overrides;
    }

    public static AgentRuntimeConfigurationResolver CreateDefault(
        string rootDirectory,
        Func<string, string?> environmentReader,
        AgentRuntimeOverrides? overrides = null)
    {
        var connectionString = FirstNonEmpty(
            overrides?.ConnectionString,
            environmentReader("ConnectionStrings__Postgres"),
            environmentReader("DRAGON_POSTGRES_CONNECTION_STRING"));

        var encryptionKey = FirstNonEmpty(
            overrides?.EncryptionKey,
            environmentReader("DRAGON_CONFIG_ENCRYPTION_KEY"));

        IAgentConfigurationStore? store = null;
        ConfigurationEncryptionService? encryptionService = null;

        if (!string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(encryptionKey))
        {
            store = new PostgresAgentConfigurationStore(connectionString);
            encryptionService = new ConfigurationEncryptionService(encryptionKey);
        }

        return new AgentRuntimeConfigurationResolver(environmentReader, store, encryptionService, overrides);
    }

    public ResolvedAgentConfiguration? Resolve(string? agentName = null)
    {
        var normalizedAgentName = Normalize(agentName);
        var cliModel = Normalize(overrides?.Model);
        var databaseAgent = ResolveDatabaseAgent(normalizedAgentName);

        if (databaseAgent is not null && !databaseAgent.Enabled)
        {
            return null;
        }

        if (cliModel is not null)
        {
            return new ResolvedAgentConfiguration(cliModel, "cli", normalizedAgentName);
        }

        if (databaseAgent?.Model is { Length: > 0 } databaseModel)
        {
            return new ResolvedAgentConfiguration(databaseModel, "database", normalizedAgentName);
        }

        var environmentConfiguration = ResolveEnvironment();
        return environmentConfiguration is null
            ? null
            : environmentConfiguration with { AgentName = normalizedAgentName };
    }

    public StoredProviderConfiguration? GetStoredProvider(string providerName) => ResolveDatabaseProvider(providerName);

    public IReadOnlyList<StoredProviderConfiguration> ListStoredProviders() => store?.ListProviders() ?? [];

    public void UpsertProvider(string name, string transport, string defaultModel, string endpoint, string apiKey)
    {
        if (store is null || encryptionService is null)
        {
            throw new InvalidOperationException("Database-backed provider configuration requires both a Postgres connection string and DRAGON_CONFIG_ENCRYPTION_KEY.");
        }

        store.UpsertProvider(new StoredProviderConfiguration(
            name,
            transport,
            defaultModel,
            endpoint,
            encryptionService.Encrypt(apiKey),
            DateTimeOffset.UtcNow));
    }

    public StoredAgentConfiguration? GetStoredAgent(string agentName) => ResolveDatabaseAgent(agentName);

    public IReadOnlyList<StoredAgentConfiguration> ListStoredAgents() => store?.ListAgents() ?? [];

    public void UpsertAgent(string agentName, string? providerName, string? model, bool enabled)
    {
        if (store is null)
        {
            throw new InvalidOperationException("Database-backed agent configuration requires a Postgres connection string.");
        }

        store.UpsertAgent(new StoredAgentConfiguration(
            agentName,
            Normalize(providerName),
            Normalize(model),
            enabled,
            DateTimeOffset.UtcNow));
    }

    private ResolvedAgentConfiguration? ResolveEnvironment()
    {
        var model = Normalize(environmentReader("CODEX_MODEL"));
        return model is null
            ? null
            : new ResolvedAgentConfiguration(model, "environment", null);
    }

    private StoredProviderConfiguration? ResolveDatabaseProvider(string? providerName)
    {
        if (store is null || string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        return store.GetProvider(providerName);
    }

    private StoredAgentConfiguration? ResolveDatabaseAgent(string? agentName)
    {
        if (store is null || string.IsNullOrWhiteSpace(agentName))
        {
            return null;
        }

        return store.GetAgent(agentName);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(Normalize).FirstOrDefault(value => value is not null);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
