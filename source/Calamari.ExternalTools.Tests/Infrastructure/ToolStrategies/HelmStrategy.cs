using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Common.Plumbing;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class HelmStrategy
    {
        public static async Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            var platform = ToolDownloader.GetPlatform();
            var arch = ToolDownloader.GetArchitecture();

            if (CalamariEnvironment.IsRunningOnWindows)
            {
                var zipFileName = $"helm-v{version}-{platform}-{arch}.zip";
                var url = $"https://get.helm.sh/{zipFileName}";
                await ToolDownloader.DownloadAndExtractZip(url, destinationDir, client);
                return Path.Combine(destinationDir, $"{platform}-{arch}", "helm.exe");
            }
            else
            {
                var tarGzFileName = $"helm-v{version}-{platform}-{arch}.tar.gz";
                var url = $"https://get.helm.sh/{tarGzFileName}";
                await ToolDownloader.DownloadAndExtractTarGz(url, destinationDir, client);
                return Path.Combine(destinationDir, $"{platform}-{arch}", "helm");
            }
        }
    }
}
