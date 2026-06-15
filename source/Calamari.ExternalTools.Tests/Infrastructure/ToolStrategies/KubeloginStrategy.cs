using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Common.Plumbing;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class KubeloginStrategy
    {
        public static async Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            var downloadUrl = GetDownloadLink(version);
            var zipFileName = GetZipFileName();

            await ToolDownloader.DownloadAndExtractZip(downloadUrl, destinationDir, client);

            return GetExecutablePath(destinationDir);
        }

        static string GetDownloadLink(string version)
        {
            if (CalamariEnvironment.IsRunningOnNix)
                return $"https://github.com/Azure/kubelogin/releases/download/{version}/kubelogin-linux-amd64.zip";

            return $"https://github.com/Azure/kubelogin/releases/download/{version}/kubelogin-win-amd64.zip";
        }

        static string GetZipFileName()
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                return "kubelogin.zip";

            return "kubelogin-linux-amd64.zip";
        }

        static string GetExecutablePath(string extractPath)
        {
            var executableName = CalamariEnvironment.IsRunningOnWindows ? "kubelogin.exe" : "kubelogin";
            var platformDir = CalamariEnvironment.IsRunningOnWindows ? "windows_amd64" : "linux_amd64";
            return Path.Combine(extractPath, "bin", platformDir, executableName);
        }
    }
}
