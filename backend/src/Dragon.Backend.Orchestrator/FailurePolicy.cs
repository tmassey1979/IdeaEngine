using Dragon.Backend.Contracts;

namespace Dragon.Backend.Orchestrator;

public static class FailurePolicy
{
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
}
