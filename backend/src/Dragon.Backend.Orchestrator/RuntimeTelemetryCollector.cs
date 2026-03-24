using System.Globalization;

namespace Dragon.Backend.Orchestrator;

internal static class RuntimeTelemetryCollector
{
    public static HostTelemetrySnapshot Collect(string rootDirectory)
    {
        int? processorCount = null;
        double? processorLoadPercent = null;
        long? memoryTotalMb = null;
        long? memoryAvailableMb = null;
        double? memoryUsedPercent = null;
        long? diskTotalGb = null;
        long? diskFreeGb = null;
        double? diskUsedPercent = null;

        try
        {
            processorCount = Environment.ProcessorCount;
        }
        catch
        {
        }

        TryReadLinuxProcessorLoad(out processorLoadPercent, processorCount);
        TryReadLinuxMemory(out memoryTotalMb, out memoryAvailableMb, out memoryUsedPercent);
        TryReadDisk(rootDirectory, out diskTotalGb, out diskFreeGb, out diskUsedPercent);

        var availableMetricCount =
            (processorCount is not null ? 1 : 0) +
            (processorLoadPercent is not null ? 1 : 0) +
            (memoryUsedPercent is not null ? 1 : 0) +
            (diskUsedPercent is not null ? 1 : 0);

        var status = availableMetricCount switch
        {
            0 => "unavailable",
            < 4 => "partial",
            _ => "available"
        };

        var summaryParts = new List<string>();
        if (processorCount is not null)
        {
            summaryParts.Add($"{processorCount.Value} core(s)");
        }

        if (processorLoadPercent is not null)
        {
            summaryParts.Add($"load {processorLoadPercent.Value.ToString("0", CultureInfo.InvariantCulture)}%");
        }

        if (memoryUsedPercent is not null && memoryAvailableMb is not null && memoryTotalMb is not null)
        {
            summaryParts.Add($"memory {memoryUsedPercent.Value.ToString("0", CultureInfo.InvariantCulture)}% used ({memoryAvailableMb.Value} MB free of {memoryTotalMb.Value} MB)");
        }

        if (diskUsedPercent is not null && diskFreeGb is not null && diskTotalGb is not null)
        {
            summaryParts.Add($"disk {diskUsedPercent.Value.ToString("0", CultureInfo.InvariantCulture)}% used ({diskFreeGb.Value} GB free of {diskTotalGb.Value} GB)");
        }

        var summary = summaryParts.Count > 0
            ? string.Join(", ", summaryParts)
            : "Host telemetry unavailable in this runtime.";

        return new HostTelemetrySnapshot(
            status,
            processorCount,
            processorLoadPercent,
            memoryTotalMb,
            memoryAvailableMb,
            memoryUsedPercent,
            diskTotalGb,
            diskFreeGb,
            diskUsedPercent,
            summary);
    }

    private static void TryReadLinuxProcessorLoad(out double? processorLoadPercent, int? processorCount)
    {
        processorLoadPercent = null;

        try
        {
            const string loadAvgPath = "/proc/loadavg";
            if (!File.Exists(loadAvgPath))
            {
                return;
            }

            var content = File.ReadAllText(loadAvgPath).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var firstToken = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!double.TryParse(firstToken, NumberStyles.Float, CultureInfo.InvariantCulture, out var loadAverage))
            {
                return;
            }

            if (processorCount is null || processorCount <= 0)
            {
                return;
            }

            processorLoadPercent = Math.Clamp(loadAverage / processorCount.Value * 100d, 0d, 100d);
        }
        catch
        {
        }
    }

    private static void TryReadLinuxMemory(out long? totalMb, out long? availableMb, out double? usedPercent)
    {
        totalMb = null;
        availableMb = null;
        usedPercent = null;

        try
        {
            const string memInfoPath = "/proc/meminfo";
            if (!File.Exists(memInfoPath))
            {
                return;
            }

            var lines = File.ReadAllLines(memInfoPath);
            var values = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                var parts = line.Split(':', 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                var digits = new string(parts[1].Where(char.IsDigit).ToArray());
                if (!long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valueKb))
                {
                    continue;
                }

                values[parts[0].Trim()] = valueKb;
            }

            if (!values.TryGetValue("MemTotal", out var memTotalKb))
            {
                return;
            }

            if (!values.TryGetValue("MemAvailable", out var memAvailableKb))
            {
                return;
            }

            totalMb = memTotalKb / 1024;
            availableMb = memAvailableKb / 1024;
            if (memTotalKb > 0)
            {
                usedPercent = Math.Clamp((memTotalKb - memAvailableKb) / (double)memTotalKb * 100d, 0d, 100d);
            }
        }
        catch
        {
        }
    }

    private static void TryReadDisk(string rootDirectory, out long? totalGb, out long? freeGb, out double? usedPercent)
    {
        totalGb = null;
        freeGb = null;
        usedPercent = null;

        try
        {
            var rootPath = Path.GetPathRoot(Path.GetFullPath(rootDirectory));
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return;
            }

            var drive = new DriveInfo(rootPath);
            if (!drive.IsReady || drive.TotalSize <= 0)
            {
                return;
            }

            totalGb = drive.TotalSize / 1024 / 1024 / 1024;
            freeGb = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
            usedPercent = Math.Clamp((drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize * 100d, 0d, 100d);
        }
        catch
        {
        }
    }
}
