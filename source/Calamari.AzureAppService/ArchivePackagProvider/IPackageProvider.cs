﻿using System.IO;
using System.Threading.Tasks;

namespace Calamari.AzureAppService
{
    public interface IPackageProvider
    {
        string UploadUrlPath { get; }

        Task<FileInfo> PackageArchive(string sourceDirectory, string targetDirectory);

        Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile);
    }
}