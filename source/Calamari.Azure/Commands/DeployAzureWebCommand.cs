using Calamari.Azure.Deployment.Conventions;
using Calamari.Shared.Commands;

namespace Calamari.Azure.Commands
{
    [DeploymentAction("deploy-azure-web", Description = "Extracts and installs a deployment package to an Azure Web Application")]
    public class DeployAzureWebCommand : Shared.Commands.IDeploymentAction
    {
        public void Build(IDeploymentStrategyBuilder deploymentStrategyBuilder)
        {
            deploymentStrategyBuilder
                .AddExtractPackageToStagingDirectory()
                .RunPreScripts()
                .AddSubsituteInFiles()
                .AddConfigurationTransform()
                .AddConfigurationVariables()
                .AddJsonVariables()
                .RunDeployScripts()
                .AddConvention<AzureWebAppConvention>()
                .RunPostScripts();
        }
    }
}