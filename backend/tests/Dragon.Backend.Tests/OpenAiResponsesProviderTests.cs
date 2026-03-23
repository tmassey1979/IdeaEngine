using Dragon.Backend.Contracts;
using Dragon.Backend.Orchestrator;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Dragon.Backend.Tests;

public sealed class OpenAiResponsesProviderTests
{
    [Fact]
    public void FromEnvironment_LoadsDefaultOpenAiConfiguration()
    {
        var options = OpenAiResponsesOptions.FromEnvironment(name => name switch
        {
            "OPENAI_API_KEY" => "test-key",
            _ => null
        });

        Assert.Equal("test-key", options.ApiKey);
        Assert.Equal("gpt-5", options.Model);
        Assert.Equal("https://api.openai.com/v1/responses", options.Endpoint);
    }

    [Fact]
    public void BuildRequestJson_UsesResponsesApiMessageShape()
    {
        var request = new AgentModelRequest(
            "developer",
            "implement",
            "gpt-5",
            "You are the developer agent.",
            [
                new AgentModelMessage("system", "Use bounded edits."),
                new AgentModelMessage("user", "Implement story #22.")
            ],
            new Dictionary<string, string>
            {
                ["issue"] = "22"
            },
            Background: true
        );

        var json = OpenAiResponsesProvider.BuildRequestJson(request);

        Assert.Contains(@"""model"":""gpt-5""", json, StringComparison.Ordinal);
        Assert.Contains(@"""instructions"":""You are the developer agent.""", json, StringComparison.Ordinal);
        Assert.Contains(@"""background"":true", json, StringComparison.Ordinal);
        Assert.Contains(@"""role"":""system""", json, StringComparison.Ordinal);
        Assert.Contains(@"""type"":""input_text""", json, StringComparison.Ordinal);
        Assert.Contains("Implement story #22.", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseResponse_ReadsTopLevelOutputText()
    {
        const string json = """
        {
          "id": "resp_123",
          "model": "gpt-5",
          "status": "completed",
          "output_text": "Planned implementation steps."
        }
        """;

        var response = OpenAiResponsesProvider.ParseResponse(json, "gpt-5");

        Assert.Equal("openai-responses", response.Provider);
        Assert.Equal("resp_123", response.ResponseId);
        Assert.Equal("gpt-5", response.Model);
        Assert.Equal("Planned implementation steps.", response.OutputText);
        Assert.Equal("completed", response.FinishReason);
    }

    [Fact]
    public void ParseResponse_FallsBackToOutputContentArray()
    {
        const string json = """
        {
          "id": "resp_456",
          "model": "gpt-5",
          "status": "completed",
          "output": [
            {
              "content": [
                {
                  "type": "output_text",
                  "text": "Recovered content from output array."
                }
              ]
            }
          ]
        }
        """;

        var response = OpenAiResponsesProvider.ParseResponse(json, "gpt-5");

        Assert.Equal("Recovered content from output array.", response.OutputText);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsProviderException_WithRetryAfterMetadata()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"error":{"message":"Rate limit reached."}}""")
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(45));
            return response;
        });
        var httpClient = new HttpClient(handler);
        var provider = new OpenAiResponsesProvider(new OpenAiResponsesOptions("test-key"), httpClient);
        var request = new AgentModelRequest(
            "architect",
            "implement_issue",
            "gpt-5",
            "You are the architect agent.",
            [new AgentModelMessage("user", "Implement story #22.")]);

        var exception = await Assert.ThrowsAsync<AgentModelProviderException>(() => provider.GenerateAsync(request));

        Assert.Equal("openai-responses", exception.Provider);
        Assert.True(exception.IsTransient);
        Assert.Equal(HttpStatusCode.TooManyRequests, exception.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(45), exception.RetryAfter);
    }

    [Fact]
    public async Task GenerateAsync_ReadsRetryAfterDateHeader_WhenDeltaIsUnavailable()
    {
        var retryAt = DateTimeOffset.UtcNow.AddSeconds(25);
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("""{"error":{"message":"Please retry later."}}""")
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAt);
            return response;
        });
        var httpClient = new HttpClient(handler);
        var provider = new OpenAiResponsesProvider(new OpenAiResponsesOptions("test-key"), httpClient);
        var request = new AgentModelRequest(
            "architect",
            "implement_issue",
            "gpt-5",
            "You are the architect agent.",
            [new AgentModelMessage("user", "Implement story #22.")]);

        var exception = await Assert.ThrowsAsync<AgentModelProviderException>(() => provider.GenerateAsync(request));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.NotNull(exception.RetryAfter);
        Assert.InRange(exception.RetryAfter!.Value.TotalSeconds, 1, 25);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
