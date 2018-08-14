using Calamari.Azure.Deployment.Conventions;
using Calamari.Azure.Util;
using Calamari.Shared;
using Calamari.Shared.Certificates;
using Calamari.Shared.Commands;

namespace Calamari.Azure.Commands
{
    [DeploymentAction("deploy-azure-service-fabric-app", Description = "Extracts and installs an Azure Service Fabric Application")]
    public class DeployAzureServiceFabricAppCommand : Shared.Commands.IDeploymentAction
    {
        private readonly ICertificateStore certificateStore;

        public DeployAzureServiceFabricAppCommand(ICertificateStore certificateStore)
        {
            this.certificateStore = certificateStore;
        }

        public void Build(IDeploymentStrategyBuilder builder)
        {
            if (!ServiceFabricHelper.IsServiceFabricSdkKeyInRegistry())
                throw new CommandException("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running Service Fabric commands.");

            builder.AddExtractPackageToStagingDirectory()
                .RunPreScripts()
                .AddSubsituteInFiles()
                .AddConfigurationTransform()
                .AddConfigurationVariables()
                .AddJsonVariables()
                .RunDeployScripts()
                .AddConvention<SubstituteVariablesInAzureServiceFabricPackageConvention>()
                .AddConvention(new EnsureCertificateInstalledInStoreConvention(certificateStore,
                    SpecialVariables.Action.ServiceFabric.ClientCertVariable,
                    SpecialVariables.Action.ServiceFabric.CertificateStoreLocation,
                    SpecialVariables.Action.ServiceFabric.CertificateStoreName))
                .AddConvention<DeployAzureServiceFabricAppConvention>()
                .RunPostScripts();
        }

    }
}
