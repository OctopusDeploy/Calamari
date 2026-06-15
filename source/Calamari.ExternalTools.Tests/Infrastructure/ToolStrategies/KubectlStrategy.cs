using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Calamari.Common.Plumbing;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class KubectlStrategy
    {
        public static async Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            var platform = ToolDownloader.GetPlatform();
            var arch = ToolDownloader.GetArchitecture();
            var fileName = CalamariEnvironment.IsRunningOnWindows ? "kubectl.exe" : "kubectl";
            var versionPrefix = version.StartsWith("v") ? "" : "v";
            var url = $"https://dl.k8s.io/release/{versionPrefix}{version}/bin/{platform}/{arch}/{fileName}";

            var destPath = Path.Combine(destinationDir, fileName);
            await ToolDownloader.DownloadFile(url, destPath, client);

            return destPath;
        }
    }
}
