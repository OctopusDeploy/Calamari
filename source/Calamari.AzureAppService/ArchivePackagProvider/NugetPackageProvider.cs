using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace Calamari.AzureAppService
{
    class NugetPackageProvider : IPackageProvider
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

        public async Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile)
        {
            var newFilePath = sourceFile.FullName.Replace(".nupkg", ".zip");
            await Task.Run(() => File.Copy(sourceFile.FullName, newFilePath));
            return new FileInfo(newFilePath);
        }
    }
}