using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Calamari.ExternalTools.Tests.Infrastructure.ToolStrategies
{
    public static class TerraformStrategy
    {
        public static async Task<string> Download(string destinationDir, string version, HttpClient client)
        {
            var platform = ToolDownloader.GetPlatform();
            var arch = ToolDownloader.GetArchitecture();
            var fileName = $"terraform_{version}_{platform}_{arch}.zip";
            var url = $"https://releases.hashicorp.com/terraform/{version}/{fileName}";

            await ToolDownloader.DownloadAndExtractZip(url, destinationDir, client);

            return Directory.EnumerateFiles(destinationDir)
                .FirstOrDefault(f => Path.GetFileName(f).Contains("terraform"))!;
        }
    }
}
