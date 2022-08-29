using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureCloudService
{
    [Command("deploy-azure-cloud-service", Description = "Extracts and installs an Azure Cloud-Service")]
    public class DeployAzureCloudServiceCommand : PipelineCommand
    {
        protected override IEnumerable<IBeforePackageExtractionBehaviour> BeforePackageExtraction(BeforePackageExtractionResolver resolver)
        {
            yield return resolver.Create<SwapAzureDeploymentBehaviour>();

        }

        protected override IEnumerable<IAfterPackageExtractionBehaviour> AfterPackageExtraction(AfterPackageExtractionResolver resolver)
        {
            yield return resolver.Create<FindCloudServicePackageBehaviour>();
            yield return resolver.Create<EnsureCloudServicePackageIsCtpFormatBehaviour>();
            yield return resolver.Create<ExtractAzureCloudServicePackageBehaviour>();
            yield return resolver.Create<ChooseCloudServiceConfigurationFileBehaviour>();
        }

        protected override IEnumerable<IPreDeployBehaviour> PreDeploy(PreDeployResolver resolver)
        {
            yield return resolver.Create<ConfigureAzureCloudServiceBehaviour>();
        }

        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<RePackageCloudServiceBehaviour>();
            yield return resolver.Create<UploadAzureCloudServicePackageBehaviour>();
            yield return resolver.Create<DeployAzureCloudServicePackageBehaviour>();
        }
    }
}