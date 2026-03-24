using System.Net;
using Dragon.Api;
using Dragon.Backend.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DragonBackendOptions>(builder.Configuration.GetSection(DragonBackendOptions.SectionName));
builder.Services.AddHttpClient<IBackendReadClient, BackendReadHttpClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DragonBackendOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds));
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/dashboard", async (IBackendReadClient backendReadClient, CancellationToken cancellationToken) =>
{
    try
    {
        var dashboard = await backendReadClient.GetDashboardAsync(cancellationToken);
        return Results.Ok(DragonApiMapper.MapDashboard(dashboard));
    }
    catch (HttpRequestException exception)
    {
        return Results.Problem(
            title: "Dashboard data is unavailable",
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/ideas", async (IBackendReadClient backendReadClient, CancellationToken cancellationToken) =>
{
    try
    {
        var ideas = await backendReadClient.GetIdeasAsync(cancellationToken);
        return Results.Ok(DragonApiMapper.MapIdeas(ideas));
    }
    catch (HttpRequestException exception)
    {
        return Results.Problem(
            title: "Idea data is unavailable",
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/ideas/{id}", async (string id, IBackendReadClient backendReadClient, CancellationToken cancellationToken) =>
{
    try
    {
        var detail = await backendReadClient.GetIdeaAsync(id, cancellationToken);
        return detail is null
            ? Results.NotFound()
            : Results.Ok(DragonApiMapper.MapIdeaDetail(detail));
    }
    catch (HttpRequestException exception)
    {
        return Results.Problem(
            title: "Idea detail is unavailable",
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/ideas/{id}/fix", async (string id, IdeaFixRequest request, IBackendReadClient backendReadClient, CancellationToken cancellationToken) =>
{
    try
    {
        var response = await backendReadClient.RequestIssueFixAsync(id, new BackendIssueFixRequest(request.OperatorInput), cancellationToken);
        return Results.Ok(DragonApiMapper.MapIssueFix(response));
    }
    catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
    {
        return Results.NotFound();
    }
    catch (HttpRequestException exception)
    {
        return Results.Problem(
            title: "Issue fix request failed",
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

public partial class Program;
