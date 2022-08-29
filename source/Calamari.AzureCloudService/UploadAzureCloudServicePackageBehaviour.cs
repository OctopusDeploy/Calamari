using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
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

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            var package = context.Variables.Get(SpecialVariables.Action.Azure.CloudServicePackagePath);
            log.InfoFormat("Uploading package to Azure blob storage: '{0}'", package);
            var packageHash = HashCalculator.Hash(package);
            var nugetPackageVersion = context.Variables.Get(PackageVariables.PackageVersion);
            var uploadedFileName = Path.ChangeExtension(Path.GetFileName(package), "." + nugetPackageVersion + "_" + packageHash + ".cspkg");

            var credentials = GetCredentials(context.Variables);

            var storageAccountName = context.Variables.Get(SpecialVariables.Action.Azure.StorageAccountName);
            var storageEndpointSuffix =
                context.Variables.Get(SpecialVariables.Action.Azure.StorageEndPointSuffix, DefaultVariables.StorageEndpointSuffix);
            var defaultServiceManagementEndpoint =
                context.Variables.Get(SpecialVariables.Action.Azure.ServiceManagementEndPoint, DefaultVariables.ServiceManagementEndpoint);
            var uploadedUri = await azurePackageUploader.Upload(credentials, storageAccountName, package, uploadedFileName,storageEndpointSuffix, defaultServiceManagementEndpoint);

            log.SetOutputVariable(SpecialVariables.Action.Azure.UploadedPackageUri, uploadedUri.ToString(), context.Variables);
            log.Info($"Package uploaded to {uploadedUri}");
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