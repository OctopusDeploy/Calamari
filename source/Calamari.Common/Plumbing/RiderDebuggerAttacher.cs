#if DEBUG
using System;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Plumbing
{
    public static class RiderDebuggerAttacher
    {
        public static bool TryAttach(int pid, ILog log)
        {
            try
            {
                var launcher = Environment.GetEnvironmentVariable("_CALAMARI_RIDER_PATH");
                if (string.IsNullOrWhiteSpace(launcher))
                    launcher = "rider";

                var solution = FindCalamariSolution();
                var arguments = solution == null
                    ? $"attach-to-process {pid}"
                    : $"attach-to-process {pid} \"{solution}\"";

                log.Info($"Asking Rider to attach to the debugger: {launcher} {arguments}");

                var result = SilentProcessRunner.ExecuteCommand(launcher, arguments, Environment.CurrentDirectory, log.Verbose, log.Verbose);
                if (result.ExitCode != 0)
                {
                    log.Warn($"Rider exited with code {result.ExitCode} when asked to attach. Attach to PID {pid} manually.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to ask Rider to attach the debugger: {ex.Message}. Attach to PID {pid} manually, or set _CALAMARI_RIDER_PATH to the Rider launcher.");
                return false;
            }
        }

        static string? FindCalamariSolution()
        {
            var overrideSolution = Environment.GetEnvironmentVariable("_CALAMARI_RIDER_SOLUTION");
            if (!string.IsNullOrWhiteSpace(overrideSolution))
                return overrideSolution;

            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            {
                var solution = Path.Combine(directory.FullName, "Calamari.sln");
                if (File.Exists(solution))
                    return solution;
            }

            return null;
        }
    }
}
#endif
