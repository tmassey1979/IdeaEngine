using Npgsql;

namespace Dragon.Backend.Orchestrator;

public sealed class PostgresAgentConfigurationStore : IAgentConfigurationStore
{
    private readonly string connectionString;

    public PostgresAgentConfigurationStore(string connectionString)
    {
        this.connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException("Missing Postgres connection string for agent/provider configuration.")
            : connectionString;
    }

    public StoredProviderConfiguration? GetProvider(string providerName)
    {
        const string sql = """
            select name, transport, default_model, endpoint, encrypted_api_key, updated_at
            from dragon_provider_configs
            where name = @name
            limit 1;
            """;

        EnsureSchema();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("name", providerName);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadProvider(reader) : null;
    }

    public IReadOnlyList<StoredProviderConfiguration> ListProviders()
    {
        const string sql = """
            select name, transport, default_model, endpoint, encrypted_api_key, updated_at
            from dragon_provider_configs
            order by name;
            """;

        EnsureSchema();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        var results = new List<StoredProviderConfiguration>();
        while (reader.Read())
        {
            results.Add(ReadProvider(reader));
        }

        return results;
    }

    public void UpsertProvider(StoredProviderConfiguration configuration)
    {
        const string sql = """
            insert into dragon_provider_configs (name, transport, default_model, endpoint, encrypted_api_key, updated_at)
            values (@name, @transport, @defaultModel, @endpoint, @encryptedApiKey, @updatedAt)
            on conflict (name) do update
            set transport = excluded.transport,
                default_model = excluded.default_model,
                endpoint = excluded.endpoint,
                encrypted_api_key = excluded.encrypted_api_key,
                updated_at = excluded.updated_at;
            """;

        EnsureSchema();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("name", configuration.Name);
        command.Parameters.AddWithValue("transport", configuration.Transport);
        command.Parameters.AddWithValue("defaultModel", configuration.DefaultModel);
        command.Parameters.AddWithValue("endpoint", configuration.Endpoint);
        command.Parameters.AddWithValue("encryptedApiKey", configuration.EncryptedApiKey);
        command.Parameters.AddWithValue("updatedAt", configuration.UpdatedAt);
        command.ExecuteNonQuery();
    }

    public StoredAgentConfiguration? GetAgent(string agentName)
    {
        const string sql = """
            select agent_name, provider_name, model, enabled, updated_at
            from dragon_agent_configs
            where agent_name = @agentName
            limit 1;
            """;

        EnsureSchema();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("agentName", agentName);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAgent(reader) : null;
    }

    public IReadOnlyList<StoredAgentConfiguration> ListAgents()
    {
        const string sql = """
            select agent_name, provider_name, model, enabled, updated_at
            from dragon_agent_configs
            order by agent_name;
            """;

        EnsureSchema();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        using var reader = command.ExecuteReader();
        var results = new List<StoredAgentConfiguration>();
        while (reader.Read())
        {
            results.Add(ReadAgent(reader));
        }

        return results;
    }

    public void UpsertAgent(StoredAgentConfiguration configuration)
    {
        const string sql = """
            insert into dragon_agent_configs (agent_name, provider_name, model, enabled, updated_at)
            values (@agentName, @providerName, @model, @enabled, @updatedAt)
            on conflict (agent_name) do update
            set provider_name = excluded.provider_name,
                model = excluded.model,
                enabled = excluded.enabled,
                updated_at = excluded.updated_at;
            """;

        EnsureSchema();
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("agentName", configuration.AgentName);
        command.Parameters.AddWithValue("providerName", (object?)configuration.ProviderName ?? DBNull.Value);
        command.Parameters.AddWithValue("model", (object?)configuration.Model ?? DBNull.Value);
        command.Parameters.AddWithValue("enabled", configuration.Enabled);
        command.Parameters.AddWithValue("updatedAt", configuration.UpdatedAt);
        command.ExecuteNonQuery();
    }

    private void EnsureSchema()
    {
        const string sql = """
            create table if not exists dragon_provider_configs (
                name text primary key,
                transport text not null,
                default_model text not null,
                endpoint text not null,
                encrypted_api_key text not null,
                updated_at timestamptz not null
            );

            create table if not exists dragon_agent_configs (
                agent_name text primary key,
                provider_name text null,
                model text null,
                enabled boolean not null,
                updated_at timestamptz not null
            );
            """;

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static StoredProviderConfiguration ReadProvider(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetFieldValue<DateTimeOffset>(5));

    private static StoredAgentConfiguration ReadAgent(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.IsDBNull(1) ? null : reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.GetBoolean(3),
        reader.GetFieldValue<DateTimeOffset>(4));
}
