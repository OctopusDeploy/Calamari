using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace Calamari.AzureAppService
{
    public abstract class BaseZipPackageProvider : IPackageProvider
    {
        public abstract string UploadUrlPath { get; }
        public abstract bool SupportsAsynchronousDeployment { get; }
        public virtual string ContentType => "application/octet-stream";

        public async Task<FileInfo> PackageArchive(string sourceDirectory, string targetDirectory)
        {
            await Task.Run(() =>
            {
                using var archive = ZipArchive.Create();
                archive.AddAllFromDirectory($"{sourceDirectory}");
                archive.SaveTo($"{targetDirectory}/app.zip", CompressionType.Deflate);
            });
            return new FileInfo($"{targetDirectory}/app.zip");
        }

        public virtual async Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile) => await Task.Run(() => sourceFile);

        protected static async Task<FileInfo> CopyNupkgToZip(FileInfo sourceFile)
        {
            var newFilePath = sourceFile.FullName.Replace(".nupkg", ".zip");
            await Task.Run(() => File.Copy(sourceFile.FullName, newFilePath));
            return new FileInfo(newFilePath);
        }
    }
}
