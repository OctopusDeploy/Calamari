using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Newtonsoft.Json.Linq;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class GCloudStrategy
    {
        public static async Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            var zipFileName = GetZipFileName(version);
            var downloadUrl = $"https://dl.google.com/dl/cloudsdk/channels/rapid/downloads/{zipFileName}";

            if (CalamariEnvironment.IsRunningOnWindows)
            {
                await ToolDownloader.DownloadAndExtractZip(downloadUrl, destinationDir, client);
            }
            else
            {
                await ToolDownloader.DownloadAndExtractTarGz(downloadUrl, destinationDir, client);
            }

            var executablePath = GetExecutablePath(destinationDir);

            if (CalamariEnvironment.IsRunningOnWindows)
            {
                InstallGkeAuthPlugin(executablePath);
            }

            return executablePath;
        }

        static string GetZipFileName(string version)
        {
            if (CalamariEnvironment.IsRunningOnNix)
                return $"google-cloud-sdk-{version}-linux-x86_64.tar.gz";
            if (CalamariEnvironment.IsRunningOnMac)
                return $"google-cloud-sdk-{version}-darwin-x86_64-bundled-python.tar.gz";

            return $"google-cloud-sdk-{version}-windows-x86_64-bundled-python.zip";
        }

        static string GetExecutablePath(string extractPath)
        {
            var executableName = CalamariEnvironment.IsRunningOnWindows ? "gcloud.cmd" : "gcloud";
            return Path.Combine(extractPath, "google-cloud-sdk", "bin", executableName);
        }

        static void InstallGkeAuthPlugin(string gcloudExecutable)
        {
            var variables = new Dictionary<string, string>();
            var pythonCopyPath = ExecuteCommandAndReturnResult(gcloudExecutable, "components copy-bundled-python", ".", variables);
            variables["CLOUDSDK_PYTHON"] = pythonCopyPath;

            var gkeComponent = ExecuteCommandAndReturnResult(
                $"\"{gcloudExecutable}\"",
                "components list --filter=\"Name=gke-gcloud-auth-plugin\" --format=\"json\"",
                ".",
                variables);

            var gkeComponentObject = JArray.Parse(gkeComponent).First();
            var installedState = gkeComponentObject["state"]!["name"]!.Value<string>();

            if (installedState != "Installed")
            {
                ExecuteCommandAndReturnResult(gcloudExecutable, "components install gke-gcloud-auth-plugin --quiet", ".", variables);
            }
        }

        static string ExecuteCommandAndReturnResult(string executable, string arguments, string workingDirectory, Dictionary<string, string>? environmentVariables = null)
        {
            var stdOut = new StringBuilder();
            var stdError = new StringBuilder();
            var exitCode = SilentProcessRunner.ExecuteCommand(
                executable,
                arguments,
                workingDirectory,
                environmentVariables ?? new Dictionary<string, string>(),
                (Action<string>)(s => stdOut.AppendLine(s)),
                (Action<string>)(s => stdError.AppendLine(s))).ExitCode;

            if (exitCode != 0)
                throw new InvalidOperationException($"Command failed (exit {exitCode}). stdOut: {stdOut}, stdError: {stdError}");

            return stdOut.ToString().Trim('\r', '\n');
        }
    }
}
