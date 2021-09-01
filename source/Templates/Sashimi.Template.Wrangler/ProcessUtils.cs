using System;
using System.Diagnostics;

namespace Sashimi.Template.Wrangler
{
    static class ProcessUtils
    {
        public static void RunDotNetCommand(string command, string workingPath, string args)
        {
            RunProcess("dotnet", workingPath, $"{command} {args}");
        }

        static void RunProcess(string name, string workingPath, string args)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(name, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    RedirectStandardError = false,
                    RedirectStandardOutput = false,
                    RedirectStandardInput = false,
                    WorkingDirectory = workingPath
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception("Non zero exit code");
        }
    }
}