using System.IO;
using Calamari.Integration.Azure;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Util;

namespace Calamari.Deployment.Conventions
{
    public class UploadAzureCloudServicePackageConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IAzurePackageUploader azurePackageUploader;
        readonly ISubscriptionCloudCredentialsFactory credentialsFactory;

        public UploadAzureCloudServicePackageConvention(ICalamariFileSystem fileSystem, IAzurePackageUploader azurePackageUploader,
            ISubscriptionCloudCredentialsFactory credentialsFactory)
        {
            this.fileSystem = fileSystem;
            this.azurePackageUploader = azurePackageUploader;
            this.credentialsFactory = credentialsFactory;
        }

        public void Install(RunningDeployment deployment)
        {
            var package = deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath); 
            Log.Info("Uploading package to Azure blob storage: '{0}'", package);
            var packageHash = Hash(package);
            var nugetPackageVersion = deployment.Variables.Get(SpecialVariables.Package.NuGetPackageVersion);
            var uploadedFileName = Path.ChangeExtension(Path.GetFileName(package), "." + nugetPackageVersion + "_" + packageHash + ".cspkg");

            var credentials = credentialsFactory.GetCredentials(
                deployment.Variables.Get(SpecialVariables.Action.Azure.SubscriptionId),
                deployment.Variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint),
                deployment.Variables.Get(SpecialVariables.Action.Azure.CertificateBytes)
                );

            var storageAccountName = deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName);

            var uploadedUri = azurePackageUploader.Upload(credentials, storageAccountName, package, uploadedFileName);

            Log.SetOutputVariable(SpecialVariables.Action.Azure.UploadedPackageUri, uploadedUri.ToString(), deployment.Variables);
            Log.Info("Package uploaded to " + uploadedUri.ToString());
        }

        string Hash(string packageFilePath)
        {
            using (var stream = fileSystem.OpenFile(packageFilePath, FileMode.Open))
            {
                return HashCalculator.Hash(stream);
            }
        }
    }
}