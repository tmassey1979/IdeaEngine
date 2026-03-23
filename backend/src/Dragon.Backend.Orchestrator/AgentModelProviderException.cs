using System.Net;

namespace Dragon.Backend.Orchestrator;

public sealed class AgentModelProviderException : Exception
{
    public AgentModelProviderException(
        string provider,
        string message,
        bool isTransient,
        HttpStatusCode? statusCode = null,
        TimeSpan? retryAfter = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Provider = provider;
        IsTransient = isTransient;
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }

    public string Provider { get; }

    public bool IsTransient { get; }

    public HttpStatusCode? StatusCode { get; }

    public TimeSpan? RetryAfter { get; }
}
