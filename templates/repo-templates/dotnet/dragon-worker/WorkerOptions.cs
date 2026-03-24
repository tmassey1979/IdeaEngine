public sealed class WorkerOptions
{
    public int PollSeconds { get; init; } = 10;
    public string QueueName { get; init; } = "dragon.jobs";
}