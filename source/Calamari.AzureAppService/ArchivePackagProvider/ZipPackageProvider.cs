namespace Calamari.AzureAppService
{
    public class ZipPackageProvider : BaseZipPackageProvider
    {
        public override string UploadUrlPath => @"/api/zipdeploy";
        public override bool SupportsAsynchronousDeployment => true;
    }
}
