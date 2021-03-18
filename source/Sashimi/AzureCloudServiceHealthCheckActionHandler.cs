using System;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.AzureCloudService
{
    class AzureCloudServiceHealthCheckActionHandler : IActionHandlerWithAccount
    {
        static readonly CalamariFlavour CalamariAzure = new CalamariFlavour("Calamari.AzureCloudService");

        public string Id => SpecialVariables.Action.Azure.CloudServiceHealthCheckActionTypeName;
        public string Name => "HealthCheck an Azure CloudService";
        public string Description => "HealthCheck an Azure CloudService.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => false;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, ActionHandlerCategory.Azure };
        public string[] StepBasedVariableNameForAccountIds { get; } = {SpecialVariables.Action.Azure.AccountId};

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            ValidateAccountIsOfType(context);

            return context.CalamariCommand(CalamariAzure, "health-check")
                .Execute(taskLog);
        }

        void ValidateAccountIsOfType(IActionHandlerContext context)
        {
            var accountType = context.Variables.Get(SpecialVariables.AccountType);
            var isLegacyStep = false;

            if (String.IsNullOrEmpty(accountType))
            {
                // This may be a legacy step, where the account was attached to the action.
                var accountId = context.Variables.Get(SpecialVariables.Action.Azure.AccountId);
                if (!String.IsNullOrEmpty(accountId))
                {
                    isLegacyStep = true;
                }
            }

            if (!isLegacyStep && accountType != AccountTypes.AzureSubscriptionAccountType.ToString())
            {
                throw new KnownDeploymentFailureException($"The account type '{accountType}' is not valid for this step.");
            }
        }
    }
}