using System.IO;
using System.Threading.Tasks;

namespace Calamari.AzureAppService
{
    // Flex Consumption requires the OneDeploy (/api/publish) endpoint, not Kudu's /api/zipdeploy.
    public class OneDeployZipPackageProvider : BaseZipPackageProvider
    {
        public override string UploadUrlPath => @"/api/publish?type=zip";
        public override bool SupportsAsynchronousDeployment => false;
        public override string ContentType => "application/zip";

        public override async Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile)
            => sourceFile.Extension == ".nupkg" ? await CopyNupkgToZip(sourceFile) : await Task.Run(() => sourceFile);
    }
}
