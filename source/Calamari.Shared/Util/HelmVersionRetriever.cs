using Calamari.Commands.Support;
using System;
using System.Text;
using Calamari.Integration.Processes;

namespace Calamari.Util
{
    public static class HelmVersionRetriever
    {
        public static HelmVersion GetVersion(string helmExecutablePath = "helm")
        {
            StringBuilder stdout = new StringBuilder();
            var result = SilentProcessRunner.ExecuteCommand(helmExecutablePath, "version --client --short", Environment.CurrentDirectory, output => stdout.AppendLine(output), error => { });
            
            if (result.ExitCode != 0)
                throw new CommandException($"Failed to retrieve version from Helm at '{helmExecutablePath}' (Exit code {result.ExitCode}). Error output: \r\n{result.ErrorOutput}");

            return HelmVersionParser.ParseVersion(stdout.ToString());
        }
    }
}
