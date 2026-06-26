using System;
using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace Calamari.AzureAppService;

public abstract class BaseZipPackageProvider : IPackageProvider
{
    public abstract string UploadUrlPath { get; }
    public abstract bool SupportsAsynchronousDeployment { get; }

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
    
    public abstract string ContentType { get; }
    public abstract string AdditionalParameters { get; }
    public string PublishingProfileMethod => "ZipDeploy";
}

public class OneDeployZipPackageProvider : BaseZipPackageProvider
{
    public override string UploadUrlPath => @"/api/publish";
    public override bool SupportsAsynchronousDeployment => false;
    public override string ContentType => @"application/zip";
    public override string AdditionalParameters => string.Empty; //"?deployer=Octopus&isAsync=true";
}