using System;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Azure.ResourceGroups
{
    class DeployBicepTemplateBehaviour : IDeployBehaviour
    {
        readonly ICommandLineRunner commandLineRunner;
        readonly TemplateService templateService;
        readonly AzureResourceGroupOperator resourceGroupOperator;
        readonly ILog log;

        public DeployBicepTemplateBehaviour(ICommandLineRunner commandLineRunner, TemplateService templateService, AzureResourceGroupOperator resourceGroupOperator, ILog log)
        {
            this.commandLineRunner = commandLineRunner;
            this.templateService = templateService;
            this.resourceGroupOperator = resourceGroupOperator;
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            var accountType = context.Variables.GetRequiredVariable(AzureScripting.SpecialVariables.Account.AccountType);
            var account = accountType == AccountType.AzureOidc.ToString() ? (IAzureAccount)new AzureOidcAccount(context.Variables) : new AzureServicePrincipalAccount(context.Variables);

            var armClient = account.CreateArmClient();

            var resourceGroupName = context.Variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupName);
            var resourceGroupLocation = context.Variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupLocation);
            var subscriptionId = context.Variables.GetRequiredVariable(AzureAccountVariables.SubscriptionId);
            var deploymentModeVariable = context.Variables.GetRequiredVariable(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode);
            var deploymentMode = (ArmDeploymentMode)Enum.Parse(typeof(ArmDeploymentMode), deploymentModeVariable);
            
            var resourceGroup = await GetOrCreateResourceGroup(armClient, subscriptionId, resourceGroupName, resourceGroupLocation);

            var (template, parameters) = GetArmTemplateAndParameters(context);

            var armDeploymentName = DeploymentName.FromStepName(context.Variables[ActionVariables.Name]);
            log.Verbose($"Deployment Name: {armDeploymentName}, set to variable \"AzureRmOutputs[DeploymentName]\"");
            log.SetOutputVariable("AzureRmOutputs[DeploymentName]", armDeploymentName, context.Variables);

            var deploymentOperation = await resourceGroupOperator.CreateDeployment(resourceGroup, armDeploymentName, deploymentMode, template, parameters);
            await resourceGroupOperator.PollForCompletion(deploymentOperation, context.Variables);
            await resourceGroupOperator.FinalizeDeployment(deploymentOperation, context.Variables);
        }

        async Task<ResourceGroupResource> GetOrCreateResourceGroup(ArmClient armClient, string subscriptionId, string resourceGroupName, string location)
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));

            var resourceGroups = subscription.GetResourceGroups();
            var existing = await resourceGroups.GetIfExistsAsync(resourceGroupName);

            if (existing.HasValue && existing.Value != null)
                return existing.Value;

            log.Info($"The resource group with the name {resourceGroupName} does not exist");
            log.Info($"Creating resource group {resourceGroupName} in location {location}");

            var resourceGroupData = new ResourceGroupData(location);
            var armOperation = await resourceGroups.CreateOrUpdateAsync(WaitUntil.Completed, resourceGroupName, resourceGroupData);
            return armOperation.Value;
        }

        (string template, string parameters) GetArmTemplateAndParameters(RunningDeployment context)
        {
            var bicepCli = new BicepCli(log, commandLineRunner, context.CurrentDirectory);

            var bicepTemplateFile = context.Variables.Get(SpecialVariables.Action.Azure.BicepTemplateFile, "template.bicep");
            var templateParametersFile = context.Variables.Get(SpecialVariables.Action.Azure.TemplateParameters, "parameters.json");
            var templateSource = context.Variables.Get(SpecialVariables.Action.Azure.TemplateSource, string.Empty);

            var filesInPackageOrRepository = templateSource == "Package" || templateSource == "GitRepository";
            if (filesInPackageOrRepository)
            {
                bicepTemplateFile = context.Variables.Get(SpecialVariables.Action.Azure.BicepTemplate);
            }

            log.Info($"Processing Bicep file: {bicepTemplateFile}");
            var armTemplateFile = bicepCli.BuildArmTemplate(bicepTemplateFile!);
            log.Info("Bicep file processed");

            var template = templateService.GetSubstitutedTemplateContent(armTemplateFile, filesInPackageOrRepository, context.Variables);
            var parameters = !string.IsNullOrWhiteSpace(templateParametersFile)
                ? templateService.GetSubstitutedTemplateContent(templateParametersFile, filesInPackageOrRepository, context.Variables)
                : null;

            return (template, parameters);
        }
    }
}