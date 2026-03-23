using System.Net;
using System.Text;
using System.Text.Json;

namespace Dragon.Backend.Orchestrator;

public sealed class StatusHttpServer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SelfBuildLoop loop;
    private readonly string? snapshotPath;

    public StatusHttpServer(SelfBuildLoop loop, string? snapshotPath = null)
    {
        this.loop = loop;
        this.snapshotPath = snapshotPath;
    }

    public async Task ServeOnceAsync(string prefix, CancellationToken cancellationToken = default)
    {
        using var listener = CreateListener(prefix);
        listener.Start();

        try
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            await HandleContextAsync(context, cancellationToken);
        }
        finally
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        }
    }

    public async Task ServeUntilCancelledAsync(string prefix, CancellationToken cancellationToken = default)
    {
        using var listener = CreateListener(prefix);
        listener.Start();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
                await HandleContextAsync(context, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        }
    }

    private static HttpListener CreateListener(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Status server prefix is required.", nameof(prefix));
        }

        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        return listener;
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            ApplyCorsHeaders(context.Response);
            var path = NormalizePath(context.Request.Url?.AbsolutePath);
            if (string.Equals(context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                await WriteTextAsync(context.Response, "Method not allowed.", "text/plain", cancellationToken);
                return;
            }

            if (string.Equals(path, "/status", StringComparison.Ordinal))
            {
                var snapshot = ReadSnapshot();

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await WriteJsonAsync(context.Response, snapshot, cancellationToken);
                return;
            }

            if (string.Equals(path, "/health", StringComparison.Ordinal))
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await WriteJsonAsync(context.Response, new { status = "ok" }, cancellationToken);
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteTextAsync(context.Response, "Not found.", "text/plain", cancellationToken);
        }
        finally
        {
            context.Response.OutputStream.Close();
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        if (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
        {
            return path.TrimEnd('/');
        }

        return path;
    }

    private StatusSnapshot ReadSnapshot()
    {
        if (!string.IsNullOrWhiteSpace(snapshotPath) && File.Exists(snapshotPath))
        {
            try
            {
                var runtimeSnapshot = JsonSerializer.Deserialize<StatusSnapshot>(
                    File.ReadAllText(snapshotPath),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (runtimeSnapshot is not null)
                {
                    return runtimeSnapshot with
                    {
                        Source = "status-http"
                    };
                }
            }
            catch (JsonException)
            {
                // A concurrent writer may have left a transiently incomplete file; fall back to the live loop snapshot.
            }
        }

        return loop.ReadStatus("serve-status", "status", "snapshot") with
        {
            Source = "status-http"
        };
    }

    private static void ApplyCorsHeaders(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object payload, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.LongLength;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task WriteTextAsync(HttpListenerResponse response, string payload, string contentType, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        response.ContentType = $"{contentType}; charset=utf-8";
        response.ContentLength64 = bytes.LongLength;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
    }
}
