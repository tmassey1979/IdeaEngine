using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static class AgentStructuredResultParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AgentStructuredResult? Parse(string? outputText)
    {
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return null;
        }

        var trimmed = outputText.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AgentStructuredResult>(trimmed, JsonOptions);
            return string.IsNullOrWhiteSpace(parsed?.Summary) ? null : parsed;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
