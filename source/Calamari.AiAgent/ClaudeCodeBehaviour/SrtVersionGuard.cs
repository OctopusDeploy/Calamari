using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AiAgent.ClaudeCodeBehaviour
{
    // Guards the Srt sandbox level: sandbox-runtime must be >= 0.0.55. Older versions fail open on
    // the network, so we fail closed if srt is missing or too old rather than run with weaker isolation.
    public static class SrtVersionGuard
    {
        public static readonly Version Minimum = new(0, 0, 55);
        static readonly Regex SemVer = new(@"(\d+)\.(\d+)\.(\d+)", RegexOptions.Compiled);

        // Extracts the first x.y.z from `srt --version` output and checks it meets the minimum.
        internal static bool MeetsMinimum(string? versionOutput, out Version? parsed)
        {
            parsed = null;
            if (string.IsNullOrWhiteSpace(versionOutput))
                return false;

            var match = SemVer.Match(versionOutput);
            if (!match.Success)
                return false;

            parsed = new Version(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value));
            return parsed >= Minimum;
        }

        // Runs `srt --version` and throws a CommandException if srt is absent or below the minimum.
        public static void Ensure(ILog log)
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
                output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
            }
            catch (Exception ex) when (ex is not CommandException)
            {
                throw new CommandException($"Could not run 'srt --version'. The Srt sandbox level requires sandbox-runtime >= {Minimum} on the worker's PATH. {ex.Message}");
            }

            if (!MeetsMinimum(output, out var parsed))
                throw new CommandException($"sandbox-runtime {(parsed?.ToString() ?? "(version not detected)")} found, but the Srt sandbox level requires >= {Minimum} (older versions fail open on the network). 'srt --version' output: {output.Trim()}");

            log.Verbose($"sandbox-runtime {parsed} satisfies the >= {Minimum} requirement.");
        }
    }
}
