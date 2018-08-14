using Calamari.Azure.Deployment.Conventions;
using Calamari.Shared.Commands;
using Octostache;

namespace Calamari.Azure.Commands
{
    [DeploymentAction("deploy-azure-resource-group", Description = "Creates a new Azure Resource Group deployment")]
    public class DeployAzureResourceGroupCommand : IDeploymentAction
    {
        public void Build(IDeploymentStrategyBuilder deploymentStrategyBuilder)
        {
            deploymentStrategyBuilder.PreExecution = MapParametersToVariables;

            deploymentStrategyBuilder
                .AddExtractPackageToStagingDirectory()
                .RunPreScripts()
                .RunDeployScripts()
                .AddConvention<DeployAzureResourceGroupConvention>()
                .RunPostScripts();
        }

        private static void MapParametersToVariables(IOptionsBuilder options, VariableDictionary dict)
        {

            //Not too sure why this was needed?
            //dict.Set(SpecialVariables.OriginalPackageDirectoryPath, Environment.CurrentDirectory);

            options.Add("template=",
                "Path to the JSON template file.",
                v => dict.Set(AzureSpecialVariables.ResourceGroupAction.Template, v));

            options.Add("templateParameters=",
                "Path to the JSON template parameters file.",
                v => dict.Set(AzureSpecialVariables.ResourceGroupAction.TemplateParameters, v));
        }
    }
}
