// ReSharper disable ClassNeverInstantiated.Global
using System;
using System.Threading.Tasks;
using Azure.ResourceManager.Resources.Models;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureResourceGroup;

class DeployAzureResourceGroupBehaviour(
    ITemplateService templateService,
    IResourceGroupTemplateNormalizer parameterNormalizer,
    ILog log,
    IAzureResourceGroupOperator azureResourceGroupOperator)
    : IDeployBehaviour
{
    public bool IsEnabled(RunningDeployment context) => true;

    public async Task Execute(RunningDeployment context)
    {
        log.Verbose("Using Modern Azure SDK behaviour.");

        var variables = context.Variables;
        var hasAccessToken = !variables.Get(AccountVariables.Jwt).IsNullOrEmpty();
        IAzureAccount account = hasAccessToken ? new AzureOidcAccount(variables) : new AzureServicePrincipalAccount(variables);

        var resourceGroupName = variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupName);
        var subscriptionId = variables.GetRequiredVariable(AzureAccountVariables.SubscriptionId);

        var deploymentNameVariable = variables[SpecialVariables.Action.Azure.ResourceGroupDeploymentName];
        var deploymentName = !string.IsNullOrWhiteSpace(deploymentNameVariable)
            ? deploymentNameVariable
            : DeploymentName.FromStepName(variables[ActionVariables.Name]);

        var deploymentModeVariable = variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode);
        var deploymentMode = (ArmDeploymentMode)Enum.Parse(typeof(ArmDeploymentMode), deploymentModeVariable);

        var templateParametersFile = variables.Get(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");
        var templateSource = variables.Get(SpecialVariables.Action.Azure.TemplateSource, string.Empty);

        var filesInPackageOrRepository = templateSource is "Package" or "GitRepository";

        var templateFile = filesInPackageOrRepository
            ? variables.GetMandatoryVariable(SpecialVariables.Action.Azure.ResourceGroupTemplate) // For now, we know that Server enforces a template when reading from a Package or Git so we treat it as mandatory
            : variables.Get(SpecialVariables.Action.Azure.Template, "template.json");
        if (filesInPackageOrRepository)
        {
            templateParametersFile = variables.Get(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters);
        }

        var template = templateService.GetSubstitutedTemplateContent(templateFile, filesInPackageOrRepository, variables);
        var parameters = !string.IsNullOrWhiteSpace(templateParametersFile)
            ? parameterNormalizer.Normalize(templateService.GetSubstitutedTemplateContent(templateParametersFile, filesInPackageOrRepository, variables))
            : null;

        await azureResourceGroupOperator.Deploy(account,
                                                subscriptionId,
                                                resourceGroupName,
                                                deploymentName,
                                                deploymentMode,
                                                template,
                                                parameters,
                                                variables);
    }
}
