using System;
using Octopus.CoreUtilities;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.AzureScripting;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureCloudService
{
    class AzureCloudServiceActionHandler : IActionHandlerWithAccount
    {
        public string Id => SpecialVariables.Action.Azure.CloudServiceActionTypeName;
        public string Name => "Deploy an Azure Cloud Service";
        public string Description => "Deploy the contents of a package to an Azure Cloud Service.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, AzureConstants.AzureActionHandlerCategory, ActionHandlerCategory.Package };
        public string[] StepBasedVariableNameForAccountIds { get; } = {SpecialVariables.Action.Azure.AccountId};

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            var isLegacyAction = !string.IsNullOrWhiteSpace(context.Variables.Get(SpecialVariables.Action.Azure.AccountId));

            if (!isLegacyAction && context.DeploymentTargetType.Some())
            {
                if (context.DeploymentTargetType.Value != AzureCloudServiceEndpoint.AzureCloudServiceDeploymentTargetType)
                    throw new ControlledActionFailedException($"The machine {context.DeploymentTargetName.SomeOr("<unknown>")} will not be deployed to because it is not an {AzureCloudServiceEndpoint.AzureCloudServiceDeploymentTargetType.DisplayName} target.");
            }

            ValidateAccountIsOfType(context, AccountTypes.AzureSubscriptionAccountType);
            AccountVariablesHaveBeenContributed(context);

            return context.CalamariCommand(AzureConstants.CalamariAzure, "deploy-azure-cloud-service")
                            .WithAzureTools(context, taskLog)
                            .WithStagedPackageArgument()
                            .Execute(taskLog);
        }

        static void AccountVariablesHaveBeenContributed(IActionHandlerContext context)
        {
            if (string.IsNullOrWhiteSpace(context.Variables.Get(SpecialVariables.Action.Azure.AccountId)) && string.IsNullOrWhiteSpace(context.Variables.Get(SpecialVariables.Account.Id)))
            {
                throw new ControlledActionFailedException("An account could not be found. Please configure an Azure target or change the step to legacy mode.");
            }
        }

        static void ValidateAccountIsOfType(IActionHandlerContext context, AccountType allowedAccountType)
        {
            var accountType = context.Variables.Get(SpecialVariables.AccountType);
            var isLegacyStep = false;
            if (String.IsNullOrEmpty(accountType))
            {
                // This may be a legacy step, where the account was attached to the action.
                var accountId = context.Variables.Get(SpecialVariables.Action.Azure.AccountId);
                if (!String.IsNullOrEmpty(accountId))
                    isLegacyStep = true;
            }
            if (!isLegacyStep && allowedAccountType.Value != accountType)
                throw new ControlledActionFailedException($"The account type '{accountType}' is not valid for this step.");
        }
    }
}