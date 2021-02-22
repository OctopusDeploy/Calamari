using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace Calamari.AzureAppService.Interfaces
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
            await Task.Run(() => System.IO.File.Move(sourceFile.FullName, newFilePath));
            return new FileInfo(newFilePath);
        }
    }
}
