using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.CommonTemp;

namespace Calamari.AzureCloudService
{
    [Command("deploy-azure-cloud-service", Description = "Extracts and installs an Azure Cloud-Service")]
    public class DeployAzureCloudServiceRegistrations : CommandPipelineRegistration
    {
        public override IEnumerable<IBeforePackageExtractionBehaviour> BeforePackageExtraction(BeforePackageExtractionResolver resolver)
        {
            yield return resolver.Create<SwapAzureDeploymentBehaviour>();
        }

        public override IEnumerable<IAfterPackageExtractionBehaviour> AfterPackageExtraction(AfterPackageExtractionResolver resolver)
        {
            yield return resolver.Create<FindCloudServicePackageBehaviour>();
            yield return resolver.Create<EnsureCloudServicePackageIsCtpFormatBehaviour>();
            yield return resolver.Create<ExtractAzureCloudServicePackageBehaviour>();
            yield return resolver.Create<ChooseCloudServiceConfigurationFileBehaviour>();
        }

        public override IEnumerable<IPreDeployBehaviour> PreDeploy(PreDeployResolver resolver)
        {
            yield return resolver.Create<ConfigureAzureCloudServiceBehaviour>();
        }

        public override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<RePackageCloudServiceBehaviour>();
            yield return resolver.Create<UploadAzureCloudServicePackageBehaviour>();
            yield return resolver.Create<DeployAzureCloudServicePackageBehaviour>();
        }
    }
}