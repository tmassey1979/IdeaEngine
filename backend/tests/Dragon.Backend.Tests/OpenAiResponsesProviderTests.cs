using Dragon.Backend.Contracts;
using Dragon.Backend.Orchestrator;

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
}
