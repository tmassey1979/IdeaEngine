using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.AddHostedService<QueueWorker>();

var host = builder.Build();
await host.RunAsync();

public sealed class QueueWorker : BackgroundService
{
    private readonly WorkerOptions options;

    public QueueWorker(IOptions<WorkerOptions> options)
    {
        this.options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(options.PollSeconds), stoppingToken);
        }
    }
}