using System.IO;
using System.Threading.Tasks;

namespace Calamari.AzureAppService
{
    public interface IPackageProvider
    {
        string UploadUrlPath { get; }

        bool SupportsAsynchronousDeployment { get; }

        // OneDeploy requires "application/zip"; the Kudu zip/war/jar endpoints accept "application/octet-stream".
        string ContentType { get; }

        Task<FileInfo> PackageArchive(string sourceDirectory, string targetDirectory);

        Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile);
    }
}