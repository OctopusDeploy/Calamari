using System;
using System.IO;
using System.Threading.Tasks;

namespace Calamari.Azure.AppServices
{
    public interface IPackageProvider
    {
        string UploadUrlPath { get; }

        bool SupportsAsynchronousDeployment { get; }

        Task<FileInfo> PackageArchive(string sourceDirectory, string targetDirectory);

        Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile);
    }
}