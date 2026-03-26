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
                "CODEX_MODEL" => "gpt-5-mini",
                _ => null
            });

        var resolved = resolver.Resolve("architect");

        Assert.NotNull(resolved);
        Assert.Equal("environment", resolved!.Source);
        Assert.Equal("gpt-5-mini", resolved.Model);
        Assert.Equal("architect", resolved.AgentName);
    }

    [Fact]
    public void Resolve_PrefersCliModelOverride_OverDatabaseConfiguration()
    {
        var encryptionService = new ConfigurationEncryptionService(ConfigurationEncryptionService.GenerateEncodedKey());
        var store = new InMemoryAgentConfigurationStore();
        store.UpsertAgent(new StoredAgentConfiguration(
            "architect",
            null,
            "gpt-5",
            true,
            DateTimeOffset.UtcNow));

        var resolver = new AgentRuntimeConfigurationResolver(
            _ => null,
            store,
            encryptionService,
            new AgentRuntimeOverrides(
                Model: "gpt-5.4-mini"));

        var resolved = resolver.Resolve("architect");

        Assert.NotNull(resolved);
        Assert.Equal("cli", resolved!.Source);
        Assert.Equal("gpt-5.4-mini", resolved.Model);
    }

    [Fact]
    public void Resolve_UsesDatabaseAgentModel_WhenCliOverridesAreAbsent()
    {
        var encryptionService = new ConfigurationEncryptionService(ConfigurationEncryptionService.GenerateEncodedKey());
        var store = new InMemoryAgentConfigurationStore();
        store.UpsertAgent(new StoredAgentConfiguration(
            "architect",
            null,
            "gpt-5.4",
            true,
            DateTimeOffset.UtcNow));

        var resolver = new AgentRuntimeConfigurationResolver(_ => null, store, encryptionService);

        var resolved = resolver.Resolve("architect");

        Assert.NotNull(resolved);
        Assert.Equal("database", resolved!.Source);
        Assert.Equal("gpt-5.4", resolved.Model);
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
