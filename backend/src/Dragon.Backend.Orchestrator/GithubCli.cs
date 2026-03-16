using System.Diagnostics;

namespace Dragon.Backend.Orchestrator;

public delegate string GithubCommandRunner(string arguments, string workingDirectory);

public static class GithubCli
{
    public static string Run(string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveExecutable(),
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start gh process.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"gh command failed: {stderr.Trim()}");
        }

        return stdout;
    }

    private static string ResolveExecutable()
    {
        var explicitPath = Environment.GetEnvironmentVariable("GH_BIN");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localBin = Path.Combine(home, ".local", "bin", "gh");
        if (File.Exists(localBin))
        {
            return localBin;
        }

        return "gh";
    }
}
