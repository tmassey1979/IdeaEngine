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
        var cliProvider = Normalize(overrides?.ProviderName);
        var cliApiKey = Normalize(overrides?.ApiKey);
        var cliModel = Normalize(overrides?.Model);
        var cliEndpoint = Normalize(overrides?.Endpoint);

        if (cliProvider is not null || cliApiKey is not null || cliModel is not null || cliEndpoint is not null)
        {
            var providerName = cliProvider ?? ResolveDatabaseProviderName(normalizedAgentName) ?? "openai-responses";
            var databaseProvider = ResolveDatabaseProvider(providerName);
            var databaseAgent = ResolveDatabaseAgent(normalizedAgentName);
            if (databaseAgent is not null && !databaseAgent.Enabled)
            {
                return null;
            }

            var apiKey = cliApiKey ?? DecryptApiKey(databaseProvider);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            return new ResolvedAgentConfiguration(
                providerName,
                apiKey,
                cliModel ?? databaseAgent?.Model ?? databaseProvider?.DefaultModel ?? "gpt-5",
                cliEndpoint ?? databaseProvider?.Endpoint ?? "https://api.openai.com/v1/responses",
                "cli",
                normalizedAgentName);
        }

        var dbProviderName = ResolveDatabaseProviderName(normalizedAgentName);
        if (dbProviderName is not null)
        {
            var databaseProvider = ResolveDatabaseProvider(dbProviderName);
            var databaseAgent = ResolveDatabaseAgent(normalizedAgentName);
            if (databaseAgent is not null && !databaseAgent.Enabled)
            {
                return null;
            }

            var apiKey = DecryptApiKey(databaseProvider);
            if (!string.IsNullOrWhiteSpace(apiKey) && databaseProvider is not null)
            {
                return new ResolvedAgentConfiguration(
                    dbProviderName,
                    apiKey,
                    databaseAgent?.Model ?? databaseProvider.DefaultModel,
                    databaseProvider.Endpoint,
                    "database",
                    normalizedAgentName);
            }
        }

        var environmentConfig = ResolveEnvironment();
        return environmentConfig is null
            ? null
            : environmentConfig with
            {
                AgentName = normalizedAgentName
            };
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
        var apiKey = Normalize(environmentReader("OPENAI_API_KEY"));
        if (apiKey is null)
        {
            return null;
        }

        return new ResolvedAgentConfiguration(
            "openai-responses",
            apiKey,
            Normalize(environmentReader("OPENAI_MODEL")) ?? "gpt-5",
            Normalize(environmentReader("OPENAI_RESPONSES_ENDPOINT")) ?? "https://api.openai.com/v1/responses",
            "environment",
            null);
    }

    private string? ResolveDatabaseProviderName(string? agentName)
    {
        var agentConfiguration = ResolveDatabaseAgent(agentName);
        if (agentConfiguration is not null)
        {
            return Normalize(agentConfiguration.ProviderName) ?? "openai-responses";
        }

        return ResolveDatabaseProvider("openai-responses") is not null ? "openai-responses" : null;
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

    private string? DecryptApiKey(StoredProviderConfiguration? configuration)
    {
        if (configuration is null)
        {
            return null;
        }

        if (encryptionService is null)
        {
            throw new InvalidOperationException("Stored provider configuration exists but DRAGON_CONFIG_ENCRYPTION_KEY is unavailable.");
        }

        return encryptionService.Decrypt(configuration.EncryptedApiKey);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(Normalize).FirstOrDefault(value => value is not null);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
