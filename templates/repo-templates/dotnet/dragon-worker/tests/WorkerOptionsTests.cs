namespace Dragon.Worker.Tests;

public sealed class WorkerOptionsTests
{
    [Fact]
    public void Defaults_AreStable()
    {
        var options = new WorkerOptions();

        Assert.Equal(10, options.PollSeconds);
        Assert.Equal("dragon.jobs", options.QueueName);
    }
}