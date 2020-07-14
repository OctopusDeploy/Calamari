using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Variables;
using Calamari.CommonTemp;
using Calamari.Deployment;
using Calamari.Util;
using Microsoft.Azure;

namespace Calamari.AzureCloudService
{
    public class UploadAzureCloudServicePackageBehaviour : IDeployBehaviour
    {
        readonly ILog log;
        readonly AzurePackageUploader azurePackageUploader;


        public UploadAzureCloudServicePackageBehaviour(ILog log, AzurePackageUploader azurePackageUploader)
        {
            this.log = log;
            this.azurePackageUploader = azurePackageUploader;
        }

        public Task Execute(RunningDeployment deployment)
        {
            var package = deployment.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath);
            log.InfoFormat("Uploading package to Azure blob storage: '{0}'", package);
            var packageHash = HashCalculator.Hash(package);
            var nugetPackageVersion = deployment.Variables.Get(PackageVariables.PackageVersion);
            var uploadedFileName = Path.ChangeExtension(Path.GetFileName(package), "." + nugetPackageVersion + "_" + packageHash + ".cspkg");

            var credentials = GetCredentials(deployment.Variables);

            var storageAccountName = deployment.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName);
            var storageEndpointSuffix =
                deployment.Variables.Get(SpecialVariables.Action.Azure.StorageEndPointSuffix, DefaultVariables.StorageEndpointSuffix);
            var defaultServiceManagementEndpoint =
                deployment.Variables.Get(SpecialVariables.Action.Azure.ServiceManagementEndPoint, DefaultVariables.ServiceManagementEndpoint);
            var uploadedUri = azurePackageUploader.Upload(credentials, storageAccountName, package, uploadedFileName,storageEndpointSuffix, defaultServiceManagementEndpoint);

            log.SetOutputVariable(SpecialVariables.Action.Azure.UploadedPackageUri, uploadedUri.ToString(), deployment.Variables);
            log.Info($"Package uploaded to {uploadedUri}");

            return this.CompletedTask();
        }

        SubscriptionCloudCredentials GetCredentials(IVariables variables)
        {
            var subscriptionId = variables.Get(SpecialVariables.Action.Azure.SubscriptionId);
            var certificateThumbprint = variables.Get(SpecialVariables.Action.Azure.CertificateThumbprint);
            var certificateBytes = Convert.FromBase64String(variables.Get(SpecialVariables.Action.Azure.CertificateBytes));

            var certificate = CalamariCertificateStore.GetOrAdd(certificateThumbprint, certificateBytes);
            return new CertificateCloudCredentials(subscriptionId, certificate);
        }
    }
}