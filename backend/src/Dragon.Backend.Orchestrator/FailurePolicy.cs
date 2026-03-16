using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static class FailurePolicy
{
    private static readonly TimeSpan LongStallThreshold = TimeSpan.FromHours(1);

    public static FailureDisposition Evaluate(IReadOnlyList<ExecutionRecord> records, int threshold = 3)
    {
        var latestFailure = records
            .OrderByDescending(record => record.RecordedAt)
            .FirstOrDefault(record => string.Equals(record.Status, "failed", StringComparison.OrdinalIgnoreCase));

        if (latestFailure is null)
        {
            return new FailureDisposition(false, null);
        }

        var repeatedAgentFailures = new List<ExecutionRecord>();
        foreach (var record in records.OrderByDescending(record => record.RecordedAt))
        {
            if (!string.Equals(record.JobAgent, latestFailure.JobAgent, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(record.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (string.Equals(record.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                repeatedAgentFailures.Add(record);
            }
        }

        if (repeatedAgentFailures.Count < threshold)
        {
            return new FailureDisposition(false, null);
        }

        return new FailureDisposition(
            true,
            $"Quarantined after {repeatedAgentFailures.Count} repeated failed {latestFailure.JobAgent} executions. Latest failure: {latestFailure.JobAgent} / {latestFailure.JobId}."
        );
    }

    public static FailureDisposition Evaluate(IssueWorkflowState workflow, DateTimeOffset? nowOverride = null)
    {
        if (string.Equals(workflow.OverallStatus, "validated", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workflow.OverallStatus, "quarantined", StringComparison.OrdinalIgnoreCase))
        {
            return new FailureDisposition(false, null);
        }

        var currentStage = InferCurrentStage(workflow);
        var currentStageState = workflow.Stages.TryGetValue(currentStage, out var stageState) ? stageState : null;
        var observedAt = currentStageState?.ObservedAt;
        if (observedAt is null)
        {
            return new FailureDisposition(false, null);
        }

        var now = nowOverride ?? DateTimeOffset.UtcNow;
        var elapsed = now - observedAt.Value;
        if (elapsed < LongStallThreshold)
        {
            return new FailureDisposition(false, null);
        }

        return new FailureDisposition(
            true,
            $"Quarantined after prolonged stall in {currentStage}. No stage progress for {FormatElapsed(elapsed)}."
        );
    }

    private static string InferCurrentStage(IssueWorkflowState workflow)
    {
        if (workflow.Stages.TryGetValue("developer", out var developer) &&
            string.Equals(developer.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return "developer";
        }

        if (!workflow.Stages.ContainsKey("developer"))
        {
            return "developer";
        }

        if (workflow.Stages.TryGetValue("review", out var review) &&
            string.Equals(review.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return "review";
        }

        if (!workflow.Stages.ContainsKey("review"))
        {
            return "review";
        }

        if (workflow.Stages.TryGetValue("test", out var test) &&
            string.Equals(test.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return "test";
        }

        if (!workflow.Stages.ContainsKey("test"))
        {
            return "test";
        }

        return "complete";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed.TotalHours >= 1)
        {
            return $"{Math.Floor(elapsed.TotalHours)}h {elapsed.Minutes}m";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            return $"{Math.Floor(elapsed.TotalMinutes)}m {elapsed.Seconds}s";
        }

        return $"{Math.Max(0, elapsed.Seconds)}s";
    }
}
