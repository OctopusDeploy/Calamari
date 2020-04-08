using System.IO;
using Calamari.Azure.CloudServices.Integration;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Util;
using SpecialVariables = Calamari.Deployment.SpecialVariables;

namespace Calamari.Azure.CloudServices.Deployment.Conventions
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
            var packageHash = HashCalculator.Hash(package);
            var nugetPackageVersion = deployment.Variables.Get(PackageVariables.PackageVersion);
            var uploadedFileName = Path.ChangeExtension(Path.GetFileName(package), "." + nugetPackageVersion + "_" + packageHash + ".cspkg");

            var credentials = credentialsFactory.GetCredentials(deployment.Variables);

            var storageAccountName = deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName);
            var storageEndpointSuffix =
                deployment.Variables.Get(SpecialVariables.Action.Azure.StorageEndPointSuffix, DefaultVariables.StorageEndpointSuffix);
            var defaultServiceManagementEndpoint =
                deployment.Variables.Get(SpecialVariables.Action.Azure.ServiceManagementEndPoint, DefaultVariables.ServiceManagementEndpoint);
            var uploadedUri = azurePackageUploader.Upload(credentials, storageAccountName, package, uploadedFileName,storageEndpointSuffix, defaultServiceManagementEndpoint);

            Log.SetOutputVariable(SpecialVariables.Action.Azure.UploadedPackageUri, uploadedUri.ToString(), deployment.Variables);
            Log.Info("Package uploaded to " + uploadedUri.ToString());
        }
    }
}