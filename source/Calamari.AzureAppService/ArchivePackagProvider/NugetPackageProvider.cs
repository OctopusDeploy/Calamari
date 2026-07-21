using System.IO;
using System.Threading.Tasks;

namespace Calamari.AzureAppService
{
    class NugetPackageProvider : BaseZipPackageProvider
    {
        public override bool SupportsAsynchronousDeployment => true;
        public override string UploadUrlPath => @"/api/zipdeploy";

        public override async Task<FileInfo> ConvertToAzureSupportedFile(FileInfo sourceFile) => await CopyNupkgToZip(sourceFile);
    }
}
