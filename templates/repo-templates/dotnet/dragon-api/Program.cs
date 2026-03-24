var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthorization();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/identity", () => Results.Ok(new { provider = "dragon" }));

app.Run();

public partial class Program;