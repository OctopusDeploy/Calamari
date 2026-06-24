using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AiAgent.ClaudeCodeBehaviour;

// sandbox-runtime must be >= 0.0.55 because older versions fail open on network access,
// allowing sandboxed processes to make outbound network connections they should not be able to.
public static class SrtVersionGuard
{
    static readonly Version Minimum = new(0, 0, 55);
    static readonly Regex SemVer = new(@"(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled);

    internal static bool MeetsMinimum(string? versionOutput, out Version? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(versionOutput))
        {
            return false;
        }

        var match = SemVer.Match(versionOutput);
        if (!match.Success)
        {
            return false;
        }

        parsed = new Version(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value));

        return parsed >= Minimum;
    }

    public static void EnsureAboveMinimum(ILog log)
    {
        string output;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "srt",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo)
                                ?? throw new CommandException($"Could not start 'srt'. The Srt sandbox level requires sandbox-runtime >= {Minimum} on the worker's PATH.");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            output = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is not CommandException)
        {
            throw new CommandException($"Could not run 'srt --version'. The Srt sandbox level requires sandbox-runtime >= {Minimum} on the worker's PATH. {ex.Message}");
        }

        if (!MeetsMinimum(output, out var parsed))
        {
            throw new CommandException($"sandbox-runtime {(parsed?.ToString() ?? "(version not detected)")} found, but the Srt sandbox level requires >= {Minimum} (older versions fail open on the network). 'srt --version' output: {output.Trim()}");
        }

        log.Verbose($"sandbox-runtime {parsed} satisfies the >= {Minimum} requirement.");
    }
}