using System;
using System.Threading.Tasks;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureResourceGroup
{
    class DeployAzureResourceGroupBehaviour : IDeployBehaviour
    {
        readonly TemplateService templateService;
        readonly IResourceGroupTemplateNormalizer parameterNormalizer;
        readonly ILog log;
        readonly AzureResourceGroupOperator azureResourceGroupOperator;

        public DeployAzureResourceGroupBehaviour(TemplateService templateService,
                                                 IResourceGroupTemplateNormalizer parameterNormalizer,
                                                 ILog log,
                                                 AzureResourceGroupOperator azureResourceGroupOperator)
        {
            this.templateService = templateService;
            this.parameterNormalizer = parameterNormalizer;
            this.log = log;
            this.azureResourceGroupOperator = azureResourceGroupOperator;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment context)
        {
            log.Verbose("Using Modern Azure SDK behaviour.");

            var variables = context.Variables;
            var hasAccessToken = !variables.Get(AccountVariables.Jwt).IsNullOrEmpty();
            var account = hasAccessToken ? (IAzureAccount)new AzureOidcAccount(variables) : new AzureServicePrincipalAccount(variables);

            var armClient = account.CreateArmClient();

            var resourceGroupName = variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupName);
            var subscriptionId = variables.GetRequiredVariable(AzureAccountVariables.SubscriptionId);

            var deploymentNameVariable = variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName];
            var deploymentName = !string.IsNullOrWhiteSpace(deploymentNameVariable)
                ? deploymentNameVariable
                : DeploymentName.FromStepName(variables[ActionVariables.Name]);

            var deploymentModeVariable = variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode);
            var deploymentMode = (ArmDeploymentMode)Enum.Parse(typeof(ArmDeploymentMode), deploymentModeVariable);

            var templateFile = variables.Get(SpecialVariables.Action.Azure.Template, "template.json");
            var templateParametersFile = variables.Get(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");
            var templateSource = variables.Get(SpecialVariables.Action.Azure.TemplateSource, string.Empty);

            var filesInPackageOrRepository = templateSource == "Package" || templateSource == "GitRepository";
            if (filesInPackageOrRepository)
            {
                templateFile = variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplate);
                templateParametersFile = variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters);
            }

            var template = templateService.GetSubstitutedTemplateContent(templateFile, filesInPackageOrRepository, variables);
            var parameters = !string.IsNullOrWhiteSpace(templateParametersFile)
                ? parameterNormalizer.Normalize(templateService.GetSubstitutedTemplateContent(templateParametersFile, filesInPackageOrRepository, variables))
                : null;

            var resourceGroupResource = armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName));
            
            log.Info($"Deploying Resource Group {resourceGroupName} in subscription {subscriptionId}.\nDeployment name: {deploymentName}\nDeployment mode: {deploymentMode}");
            
            var deploymentOperation = await azureResourceGroupOperator.CreateDeployment(resourceGroupResource,
                                                                                        deploymentName,
                                                                                        deploymentMode,
                                                                                        template,
                                                                                        parameters);
            await azureResourceGroupOperator.PollForCompletion(deploymentOperation, variables);
            await azureResourceGroupOperator.FinalizeDeployment(deploymentOperation, variables);
        }
    }
}