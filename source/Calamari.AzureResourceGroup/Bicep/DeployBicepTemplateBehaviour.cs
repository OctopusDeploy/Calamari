using System;
using System.Threading.Tasks;
using Azure.ResourceManager.Resources.Models;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureResourceGroup.Bicep;

// ReSharper disable once ClassNeverInstantiated.Global
class DeployBicepTemplateBehaviour(IBicepCompiler bicepCompiler, ITemplateService templateService, IAzureResourceGroupOperator resourceGroupOperator, ILog log)
    : IDeployBehaviour
{
    public bool IsEnabled(RunningDeployment context)
    {
        return true;
    }

    public async Task Execute(RunningDeployment context)
    {
        var accountType = context.Variables.GetRequiredVariable(AzureScripting.SpecialVariables.Account.AccountType);
        IAzureAccount account = accountType == nameof(AccountType.AzureOidc) ? new AzureOidcAccount(context.Variables) : new AzureServicePrincipalAccount(context.Variables);

        var resourceGroupName = context.Variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupName);
        var resourceGroupLocation = context.Variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupLocation);
        var subscriptionId = context.Variables.GetRequiredVariable(AzureAccountVariables.SubscriptionId);
        var deploymentModeVariable = context.Variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode);
        var deploymentMode = (ArmDeploymentMode)Enum.Parse(typeof(ArmDeploymentMode), deploymentModeVariable);

        var (template, parameters) = GetArmTemplateAndParameters(context);

        var armDeploymentName = DeploymentName.FromStepName(context.Variables[ActionVariables.Name]);
        log.Verbose($"Deployment Name: {armDeploymentName}, set to variable \"AzureRmOutputs[DeploymentName]\"");
        log.SetOutputVariable("AzureRmOutputs[DeploymentName]", armDeploymentName, context.Variables);

        await resourceGroupOperator.DeployCreatingResourceGroup(account,
                                                                subscriptionId,
                                                                resourceGroupName,
                                                                resourceGroupLocation,
                                                                armDeploymentName,
                                                                deploymentMode,
                                                                template,
                                                                parameters,
                                                                context.Variables);
    }

    (string template, string? parameters) GetArmTemplateAndParameters(RunningDeployment context)
    {
        var bicepTemplateFile = context.Variables.Get(SpecialVariables.Action.Azure.BicepTemplateFile, "template.bicep");
        var templateSource = context.Variables.Get(SpecialVariables.Action.Azure.TemplateSource, string.Empty);

        var filesInPackageOrRepository = templateSource is "Package" or "GitRepository";
        if (filesInPackageOrRepository)
        {
            bicepTemplateFile = context.Variables.Get(SpecialVariables.Action.Azure.BicepTemplate);
        }

        log.Info($"Processing Bicep file: {bicepTemplateFile}");
        var armTemplateFile = bicepCompiler.BuildArmTemplate(context.CurrentDirectory, bicepTemplateFile!);
        log.Info("Bicep file processed");

        var template = templateService.GetSubstitutedTemplateContent(armTemplateFile, filesInPackageOrRepository, context.Variables);

        var parametersValue = context.Variables.GetRaw(SpecialVariables.Action.Azure.BicepTemplateParameters) ?? string.Empty;

        var parameters = BicepToArmParameterMapper.Map(parametersValue, template, context.Variables);

        return (template, parameters);
    }
}
