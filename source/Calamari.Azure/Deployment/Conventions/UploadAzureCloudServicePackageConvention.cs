using System.IO;
using Calamari.Azure.Integration;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Util;

namespace Calamari.Azure.Deployment.Conventions
{
    public class UploadAzureCloudServicePackageConvention : IConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly IAzurePackageUploader azurePackageUploader;
        readonly ISubscriptionCloudCredentialsFactory credentialsFactory;
        private readonly ILog log = Log.Instance;


        public UploadAzureCloudServicePackageConvention(ICalamariFileSystem fileSystem, IAzurePackageUploader azurePackageUploader,
            ISubscriptionCloudCredentialsFactory credentialsFactory)
        {
            this.fileSystem = fileSystem;
            this.azurePackageUploader = azurePackageUploader;
            this.credentialsFactory = credentialsFactory;
        }

        public void Run(IExecutionContext deployment)
        {
            var package = deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath); 
            log.InfoFormat("Uploading package to Azure blob storage: '{0}'", package);
            var packageHash = HashCalculator.Hash(package);
            var nugetPackageVersion = deployment.Variables.Get(SpecialVariables.Package.NuGetPackageVersion);
            var uploadedFileName = Path.ChangeExtension(Path.GetFileName(package), "." + nugetPackageVersion + "_" + packageHash + ".cspkg");

            var credentials = credentialsFactory.GetCredentials(deployment.Variables);

            var storageAccountName = deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName);
            var storageEndpointSuffix =
                deployment.Variables.Get(SpecialVariables.Action.Azure.StorageEndPointSuffix, DefaultVariables.StorageEndpointSuffix);
            var defaultServiceManagementEndpoint =
                deployment.Variables.Get(SpecialVariables.Action.Azure.ServiceManagementEndPoint, DefaultVariables.ServiceManagementEndpoint);
            var uploadedUri = azurePackageUploader.Upload(credentials, storageAccountName, package, uploadedFileName,storageEndpointSuffix, defaultServiceManagementEndpoint);

            log.SetOutputVariable(SpecialVariables.Action.Azure.UploadedPackageUri, uploadedUri.ToString(), deployment.Variables);
            log.Info("Package uploaded to " + uploadedUri.ToString());
        }
    }
}