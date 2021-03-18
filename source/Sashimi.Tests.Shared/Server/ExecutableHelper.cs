using System;
using System.IO;
using System.Text;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;

namespace Sashimi.Tests.Shared.Server
{
    public static class ExecutableHelper
    {
        public static void AddExecutePermission(string exePath)
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                return;

            var stdOut = new StringBuilder();
            var stdError = new StringBuilder();
            var result = SilentProcessRunner.ExecuteCommand("chmod",
                                                            $"+x {exePath}",
                                                            Path.GetDirectoryName(exePath) ?? string.Empty,
                                                            s => stdOut.AppendLine(s),
                                                            s => stdError.AppendLine(s));

            if (result.ExitCode != 0)
                throw new Exception(stdOut.ToString() + stdError);
        }
    }
}