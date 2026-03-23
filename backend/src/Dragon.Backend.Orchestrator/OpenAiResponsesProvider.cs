using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public sealed record OpenAiResponsesOptions(
    string ApiKey,
    string Model = "gpt-5",
    string Endpoint = "https://api.openai.com/v1/responses",
    string ApiKeyEnvironmentVariable = "OPENAI_API_KEY"
)
{
    public static OpenAiResponsesOptions FromEnvironment(Func<string, string?> environmentReader)
    {
        var apiKey = environmentReader("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Missing OPENAI_API_KEY environment variable.");
        }

        var model = environmentReader("OPENAI_MODEL");
        var endpoint = environmentReader("OPENAI_RESPONSES_ENDPOINT");

        return new OpenAiResponsesOptions(
            apiKey,
            string.IsNullOrWhiteSpace(model) ? "gpt-5" : model,
            string.IsNullOrWhiteSpace(endpoint) ? "https://api.openai.com/v1/responses" : endpoint
        );
    }
}

public sealed class OpenAiResponsesProvider : IAgentModelProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient httpClient;
    private readonly OpenAiResponsesOptions options;

    public OpenAiResponsesProvider(OpenAiResponsesOptions options, HttpClient? httpClient = null)
    {
        this.options = options;
        this.httpClient = httpClient ?? new HttpClient();
    }

    public AgentModelProviderDescriptor Describe() => new(
        "openai-responses",
        "https",
        options.Model,
        options.ApiKeyEnvironmentVariable,
        "API-first provider for unattended agent execution using the OpenAI Responses API."
    );

    public async Task<AgentModelResponse> GenerateAsync(AgentModelRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, options.Endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        message.Content = new StringContent(BuildRequestJson(request), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateProviderException(response, responseJson);
        }

        return ParseResponse(responseJson, request.Model);
    }

    public static string BuildRequestJson(AgentModelRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = request.Model,
            ["instructions"] = request.Instructions,
            ["background"] = request.Background,
            ["input"] = request.Messages.Select(message => new Dictionary<string, object?>
            {
                ["type"] = "message",
                ["role"] = message.Role,
                ["content"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = "input_text",
                        ["text"] = message.Content
                    }
                }
            }).ToArray(),
            ["metadata"] = request.Metadata
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static AgentModelResponse ParseResponse(string json, string fallbackModel)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var responseId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
        var model = root.TryGetProperty("model", out var modelElement) ? modelElement.GetString() ?? fallbackModel : fallbackModel;
        var finishReason = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;

        if (root.TryGetProperty("output_text", out var outputTextElement) && outputTextElement.ValueKind == JsonValueKind.String)
        {
            return new AgentModelResponse("openai-responses", model, responseId, outputTextElement.GetString() ?? string.Empty, finishReason);
        }

        var outputText = TryReadOutputText(root);
        return new AgentModelResponse("openai-responses", model, responseId, outputText, finishReason);
    }

    private static string TryReadOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var outputElement) || outputElement.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        foreach (var outputItem in outputElement.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in contentElement.EnumerateArray())
            {
                if (!contentItem.TryGetProperty("type", out var typeElement) ||
                    !string.Equals(typeElement.GetString(), "output_text", StringComparison.Ordinal))
                {
                    continue;
                }

                if (contentItem.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String)
                {
                    return textElement.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static AgentModelProviderException CreateProviderException(HttpResponseMessage response, string responseBody)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        var reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
            ? response.StatusCode.ToString()
            : response.ReasonPhrase;
        var details = string.IsNullOrWhiteSpace(responseBody)
            ? null
            : responseBody.Trim();
        var message = details is null
            ? $"OpenAI Responses request failed with HTTP {(int)response.StatusCode} ({reason})."
            : $"OpenAI Responses request failed with HTTP {(int)response.StatusCode} ({reason}): {details}";

        return new AgentModelProviderException(
            "openai-responses",
            message,
            IsTransientStatusCode(response.StatusCode),
            response.StatusCode,
            retryAfter);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.RequestTimeout => true,
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            var code when code >= HttpStatusCode.InternalServerError => true,
            _ => false
        };
    }
}
