using System.IO;
using System.Threading.Tasks;

namespace Calamari.AzureAppService
{
    // Flex Consumption plans don't expose Kudu's /api/zipdeploy endpoint; they require the OneDeploy
    // (/api/publish) endpoint. This runs synchronously, mirroring the existing .jar (/api/publish?type=jar) path.
    public class OneDeployZipPackageProvider : BaseZipPackageProvider
    {
        public override string UploadUrlPath => @"/api/publish?type=zip";
        public override bool SupportsAsynchronousDeployment => false;
        public override string ContentType => "application/zip";

        public override async Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile)
            => sourceFile.Extension == ".nupkg" ? await CopyNupkgToZip(sourceFile) : await Task.Run(() => sourceFile);
    }
}
