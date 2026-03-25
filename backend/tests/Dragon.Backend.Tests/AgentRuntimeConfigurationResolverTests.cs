using Dragon.Backend.Orchestrator;

namespace Dragon.Backend.Tests;

public sealed class AgentRuntimeConfigurationResolverTests
{
    [Fact]
    public void Resolve_UsesEnvironmentFallback_WhenDatabaseIsUnavailable()
    {
        var resolver = AgentRuntimeConfigurationResolver.CreateDefault(
            CreateTempRoot(),
            name => name switch
            {
                "OPENAI_API_KEY" => "env-key",
                "OPENAI_MODEL" => "gpt-5-mini",
                "OPENAI_RESPONSES_ENDPOINT" => "https://example.invalid/responses",
                _ => null
            });

        var resolved = resolver.Resolve("architect");

        Assert.NotNull(resolved);
        Assert.Equal("environment", resolved!.Source);
        Assert.Equal("env-key", resolved.ApiKey);
        Assert.Equal("gpt-5-mini", resolved.Model);
        Assert.Equal("https://example.invalid/responses", resolved.Endpoint);
        Assert.Equal("architect", resolved.AgentName);
    }

    [Fact]
    public void Resolve_PrefersCliOverrides_OverDatabaseConfiguration()
    {
        var encryptionService = new ConfigurationEncryptionService(ConfigurationEncryptionService.GenerateEncodedKey());
        var store = new InMemoryAgentConfigurationStore();
        store.UpsertProvider(new StoredProviderConfiguration(
            "openai-responses",
            "responses",
            "gpt-5",
            "https://database.example/responses",
            encryptionService.Encrypt("db-key"),
            DateTimeOffset.UtcNow));
        store.UpsertAgent(new StoredAgentConfiguration(
            "architect",
            "openai-responses",
            "gpt-5",
            true,
            DateTimeOffset.UtcNow));

        var resolver = new AgentRuntimeConfigurationResolver(
            _ => null,
            store,
            encryptionService,
            new AgentRuntimeOverrides(
                ProviderName: "openai-responses",
                ApiKey: "cli-key",
                Model: "gpt-5.4-mini",
                Endpoint: "https://cli.example/responses"));

        var resolved = resolver.Resolve("architect");

        Assert.NotNull(resolved);
        Assert.Equal("cli", resolved!.Source);
        Assert.Equal("cli-key", resolved.ApiKey);
        Assert.Equal("gpt-5.4-mini", resolved.Model);
        Assert.Equal("https://cli.example/responses", resolved.Endpoint);
    }

    [Fact]
    public void Resolve_UsesEncryptedDatabaseConfiguration_WhenCliOverridesAreAbsent()
    {
        var encryptionService = new ConfigurationEncryptionService(ConfigurationEncryptionService.GenerateEncodedKey());
        var store = new InMemoryAgentConfigurationStore();
        store.UpsertProvider(new StoredProviderConfiguration(
            "openai-responses",
            "responses",
            "gpt-5",
            "https://database.example/responses",
            encryptionService.Encrypt("db-key"),
            DateTimeOffset.UtcNow));
        store.UpsertAgent(new StoredAgentConfiguration(
            "architect",
            "openai-responses",
            "gpt-5.4",
            true,
            DateTimeOffset.UtcNow));

        var resolver = new AgentRuntimeConfigurationResolver(_ => null, store, encryptionService);

        var resolved = resolver.Resolve("architect");

        Assert.NotNull(resolved);
        Assert.Equal("database", resolved!.Source);
        Assert.Equal("db-key", resolved.ApiKey);
        Assert.Equal("gpt-5.4", resolved.Model);
        Assert.Equal("https://database.example/responses", resolved.Endpoint);
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragon-config-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class InMemoryAgentConfigurationStore : IAgentConfigurationStore
    {
        private readonly Dictionary<string, StoredProviderConfiguration> providers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, StoredAgentConfiguration> agents = new(StringComparer.OrdinalIgnoreCase);

        public StoredProviderConfiguration? GetProvider(string providerName) =>
            providers.TryGetValue(providerName, out var provider) ? provider : null;

        public IReadOnlyList<StoredProviderConfiguration> ListProviders() =>
            providers.Values.OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        public void UpsertProvider(StoredProviderConfiguration configuration) => providers[configuration.Name] = configuration;

        public StoredAgentConfiguration? GetAgent(string agentName) =>
            agents.TryGetValue(agentName, out var agent) ? agent : null;

        public IReadOnlyList<StoredAgentConfiguration> ListAgents() =>
            agents.Values.OrderBy(value => value.AgentName, StringComparer.OrdinalIgnoreCase).ToArray();

        public void UpsertAgent(StoredAgentConfiguration configuration) => agents[configuration.AgentName] = configuration;
    }
}
