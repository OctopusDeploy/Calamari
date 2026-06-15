using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class AwsCliStrategy
    {
        public static async Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            if (CalamariEnvironment.IsRunningOnMac)
            {
                // For Mac, assume aws CLI is installed manually and on PATH
                return "aws";
            }

            var downloadUrl = GetDownloadLink(version);
            var fileName = GetFileName();
            var installerPath = Path.Combine(destinationDir, fileName);

            await ToolDownloader.DownloadFile(downloadUrl, installerPath, client);

            if (CalamariEnvironment.IsRunningOnWindows)
            {
                var extractDir = Path.Combine(destinationDir, "extract");
                if (!Directory.Exists(extractDir))
                {
                    ExecuteCommand("msiexec",
                        $"/a {installerPath} /qn TARGETDIR={extractDir}",
                        destinationDir);
                }

                return Path.Combine(extractDir, "Amazon", "AWSCLIV2", "aws.exe");
            }
            else
            {
                // Linux
                ExecuteCommand("sudo", "apt-get install zip", destinationDir);
                ExecuteCommand("unzip",
                    $"{installerPath} -d {destinationDir}",
                    destinationDir);

                return Path.Combine(destinationDir, "aws", "dist", "aws");
            }
        }

        static string GetDownloadLink(string version)
        {
            var versionString = version != "latest" ? $"-{version}" : "";
            if (CalamariEnvironment.IsRunningOnNix)
                return $"https://awscli.amazonaws.com/awscli-exe-linux-x86_64{versionString}.zip";
            if (CalamariEnvironment.IsRunningOnMac)
                return $"https://awscli.amazonaws.com/AWSCLIV2{versionString}.pkg";

            return $"https://awscli.amazonaws.com/AWSCLIV2{versionString}.msi";
        }

        static string GetFileName()
        {
            if (CalamariEnvironment.IsRunningOnNix)
                return "awscli-exe-linux-x86_64.zip";
            if (CalamariEnvironment.IsRunningOnMac)
                return "AWSCLIV2.pkg";

            return "AWSCLIV2.msi";
        }

        static void ExecuteCommand(string executable, string arguments, string workingDirectory)
        {
            var stdOut = new StringBuilder();
            var stdError = new StringBuilder();
            var exitCode = SilentProcessRunner.ExecuteCommand(
                executable,
                arguments,
                workingDirectory,
                new Dictionary<string, string>(),
                (Action<string>)(s => stdOut.AppendLine(s)),
                (Action<string>)(s => stdError.AppendLine(s))).ExitCode;

            if (exitCode != 0)
                throw new InvalidOperationException($"Command failed (exit {exitCode}). stdOut: {stdOut}, stdError: {stdError}");
        }
    }
}
