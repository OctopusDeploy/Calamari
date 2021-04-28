using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace Calamari.AzureAppService
{
    public class ZipPackageProvider : IPackageProvider
    {
        public string UploadUrlPath => @"/api/zipdeploy";

        public async Task<FileInfo> PackageArchive(string sourceDirectory, string targetDirectory)
        {
            await Task.Run(() =>
            {
                using var archive = ZipArchive.Create();
                archive.AddAllFromDirectory(
                    $"{sourceDirectory}");
                archive.SaveTo($"{targetDirectory}/app.zip", CompressionType.Deflate);
            });
            return new FileInfo($"{targetDirectory}/app.zip");
        }

        public async Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile) => await Task.Run(() => sourceFile);
    }
}