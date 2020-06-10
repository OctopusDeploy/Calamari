using Sashimi.Azure.Accounts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppHealthCheckActionHandler : IActionHandlerWithAccount
    {
        static readonly CalamariFlavour CalamariAzure = new CalamariFlavour("Calamari.AzureWebApp");

        public string Id => SpecialVariables.Action.Azure.WebAppHealthCheckActionTypeName;
        public string Name => "HealthCheck an Azure Web App";
        public string Description => "HealthCheck an Azure Web App.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => false;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, ActionHandlerCategory.Azure };
        public string[] StepBasedVariableNameForAccountIds { get; } = {SpecialVariables.Action.Azure.AccountId};

        public IActionHandlerResult Execute(IActionHandlerContext context)
        {
            ValidateAccountIsNotManagementCertificate(context);

            return context.CalamariCommand(CalamariAzure, "health-check")
                .Execute();
        }

        void ValidateAccountIsNotManagementCertificate(IActionHandlerContext context)
        {
            if (context.Variables.Get(SpecialVariables.AccountType) != AccountTypes.AzureServicePrincipalAccountType.ToString())
            {
                context.Log.Warn("Azure have announced they will be retiring Service Management API support on June 30th 2018. Please switch to using Service Principals for your Octopus Azure accounts https://g.octopushq.com/AzureServicePrincipalAccount");
            }
        }
    }
}