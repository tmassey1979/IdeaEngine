using System.Net;
using System.Text;
using System.Text.Json;
using Dragon.Backend.Contracts;

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
    private readonly StatusReadModelBuilder readModelBuilder;
    private readonly WorkflowStateStore workflowStateStore;
    private readonly QueueStore queueStore;
    private readonly ExecutionRecordStore executionRecordStore;

    public StatusHttpServer(SelfBuildLoop loop, string? snapshotPath = null)
    {
        this.loop = loop;
        this.snapshotPath = snapshotPath;
        readModelBuilder = new StatusReadModelBuilder(loop.RootDirectory);
        workflowStateStore = new WorkflowStateStore(loop.RootDirectory);
        queueStore = new QueueStore(loop.RootDirectory);
        executionRecordStore = new ExecutionRecordStore(loop.RootDirectory);
    }

    public async Task ServeOnceAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var listener = CreateListener(prefix);
        listener.Start();

        try
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            await HandleContextAsync(context, cancellationToken);
            await Task.Delay(50, CancellationToken.None);
        }
        finally
        {
            if (listener is not null)
            {
                listener.Close();
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
        var responseClosed = false;

        try
        {
            ApplyCorsHeaders(context.Response);
            var path = NormalizePath(context.Request.Url?.AbsolutePath);
            if (string.Equals(context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                context.Response.Close();
                responseClosed = true;
                return;
            }

            if (string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase) &&
                TryMatchIssueFixPath(path, out var fixIssueNumber))
            {
                var payload = await ReadJsonAsync<BackendIssueFixRequest>(context.Request, cancellationToken) ??
                    new BackendIssueFixRequest(null);

                try
                {
                    var response = loop.RequestIssueFix(fixIssueNumber, payload.OperatorInput);
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await WriteJsonAsync(context.Response, response, cancellationToken);
                    responseClosed = true;
                }
                catch (KeyNotFoundException)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    await WriteTextAsync(context.Response, "Issue not found.", "text/plain", cancellationToken);
                    responseClosed = true;
                }

                return;
            }

            if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                await WriteTextAsync(context.Response, "Method not allowed.", "text/plain", cancellationToken);
                responseClosed = true;
                return;
            }

            if (string.Equals(path, "/status", StringComparison.Ordinal))
            {
                var snapshot = ReadSnapshot();

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await WriteJsonAsync(context.Response, snapshot, cancellationToken);
                responseClosed = true;
                return;
            }

            if (string.Equals(path, "/health", StringComparison.Ordinal))
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await WriteJsonAsync(context.Response, new { status = "ok" }, cancellationToken);
                responseClosed = true;
                return;
            }

            if (string.Equals(path, "/api/read/dashboard", StringComparison.Ordinal))
            {
                var snapshot = ReadSnapshot();
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await WriteJsonAsync(context.Response, readModelBuilder.BuildDashboard(snapshot), cancellationToken);
                responseClosed = true;
                return;
            }

            if (string.Equals(path, "/api/read/issues", StringComparison.Ordinal))
            {
                var snapshot = ReadSnapshot();
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await WriteJsonAsync(context.Response, readModelBuilder.BuildIssues(snapshot), cancellationToken);
                responseClosed = true;
                return;
            }

            if (TryMatchIssueDetailPath(path, out var issueNumber))
            {
                var snapshot = ReadSnapshot();
                var detail = readModelBuilder.BuildIssueDetail(snapshot, issueNumber);
                if (detail is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    await WriteTextAsync(context.Response, "Issue not found.", "text/plain", cancellationToken);
                    responseClosed = true;
                    return;
                }

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await WriteJsonAsync(context.Response, detail, cancellationToken);
                responseClosed = true;
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteTextAsync(context.Response, "Not found.", "text/plain", cancellationToken);
            responseClosed = true;
        }
        finally
        {
            if (!responseClosed)
            {
                context.Response.Close();
            }
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

    private static bool TryMatchIssueDetailPath(string path, out int issueNumber)
    {
        issueNumber = 0;
        const string prefix = "/api/read/issues/";

        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var issueText = path[prefix.Length..];
        return int.TryParse(issueText, out issueNumber);
    }

    private static bool TryMatchIssueFixPath(string path, out int issueNumber)
    {
        issueNumber = 0;
        const string prefix = "/api/control/issues/";
        const string suffix = "/fix";

        if (!path.StartsWith(prefix, StringComparison.Ordinal) || !path.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var issueText = path[prefix.Length..^suffix.Length];
        return int.TryParse(issueText, out issueNumber);
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
                    if (!IsRuntimeSnapshotStale(runtimeSnapshot, snapshotPath))
                    {
                        return runtimeSnapshot with
                        {
                            Source = "status-http"
                        };
                    }

                    return loop.ReadStatus(
                        runtimeSnapshot.LastCommand,
                        runtimeSnapshot.WorkerMode,
                        runtimeSnapshot.WorkerState,
                        runtimeSnapshot.WorkerCompletionReason,
                        runtimeSnapshot.NextPollAt,
                        runtimeSnapshot.PollIntervalSeconds,
                        runtimeSnapshot.IdleStreak,
                        runtimeSnapshot.IdleTarget,
                        runtimeSnapshot.IdlePassesRemaining,
                        runtimeSnapshot.PassBudgetRemaining,
                        runtimeSnapshot.LatestPass,
                        runtimeSnapshot.CurrentPassNumber,
                        runtimeSnapshot.MaxPasses,
                        runtimeSnapshot.WorkerActivity) with
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

    private bool IsRuntimeSnapshotStale(StatusSnapshot runtimeSnapshot, string runtimeSnapshotPath)
    {
        var snapshotTimestamp = new[]
        {
            runtimeSnapshot.GeneratedAt,
            ReadFileWriteTimeUtc(runtimeSnapshotPath)
        }
        .Where(timestamp => timestamp is not null)
        .Select(timestamp => timestamp!.Value)
        .DefaultIfEmpty(runtimeSnapshot.GeneratedAt)
        .Max();

        var latestMutation = new[]
        {
            ReadFileWriteTimeUtc(workflowStateStore.StatePath),
            ReadFileWriteTimeUtc(queueStore.QueuePath),
            ReadLatestRunWriteTimeUtc()
        }
        .Where(timestamp => timestamp is not null)
        .Select(timestamp => timestamp!.Value)
        .DefaultIfEmpty(DateTimeOffset.MinValue)
        .Max();

        return latestMutation > snapshotTimestamp;
    }

    private DateTimeOffset? ReadLatestRunWriteTimeUtc()
    {
        if (!Directory.Exists(executionRecordStore.RunsDirectory))
        {
            return null;
        }

        return Directory.GetFiles(executionRecordStore.RunsDirectory, "issue-*.json", SearchOption.TopDirectoryOnly)
            .Select(ReadFileWriteTimeUtc)
            .Where(timestamp => timestamp is not null)
            .Select(timestamp => timestamp!.Value)
            .DefaultIfEmpty()
            .Max();
    }

    private static DateTimeOffset? ReadFileWriteTimeUtc(string path)
    {
        return File.Exists(path)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero)
            : null;
    }

    private static void ApplyCorsHeaders(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    }

    private static async Task<TPayload?> ReadJsonAsync<TPayload>(HttpListenerRequest request, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        return JsonSerializer.Deserialize<TPayload>(content, SerializerOptions);
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object payload, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.LongLength;
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        response.Close(bytes, willBlock: true);
    }

    private static async Task WriteTextAsync(HttpListenerResponse response, string payload, string contentType, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        response.ContentType = $"{contentType}; charset=utf-8";
        response.ContentLength64 = bytes.LongLength;
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        response.Close(bytes, willBlock: true);
    }
}
