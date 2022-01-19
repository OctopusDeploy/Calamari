using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureScripting;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.AzureAppService
{
    class AzureWebAppHealthCheckActionHandler : IActionHandlerWithAccount
    {
        static readonly CalamariFlavour CalamariAzureAppService = new CalamariFlavour("Calamari.AzureAppService");

        public string Id => SpecialVariables.Action.Azure.WebAppHealthCheckActionTypeName;
        public string Name => "HealthCheck an Azure Web App";
        public string Description => "HealthCheck an Azure Web App.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => false;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, ActionHandlerCategory.Azure };
        public string[] StepBasedVariableNameForAccountIds { get; } = {SpecialVariables.Action.Azure.AccountId};

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            return context.CalamariCommand(CalamariAzureAppService, "health-check")
                          .WithCheckAccountIsNotManagementCertificate(context, taskLog)
                          .Execute(taskLog);
        }
    }

    class AzureWebAppDiscoveryActionHandler : IActionHandler
    {
        static readonly CalamariFlavour CalamariAzureAppService = new CalamariFlavour("Calamari.AzureAppService");

        public string Id => SpecialVariables.Action.Azure.WebAppHealthCheckActionTypeName;
        public string Name => "Discover Azure Web Apps";
        public string Description => "Discover available Azure Web Apps.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => false;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, ActionHandlerCategory.Azure };

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            taskLog.Info($"Yo I'm finding some web apps here using context '{context.Variables.Get("Octopus.DiscoverTargets.Context")}'");
            return ActionHandlerResult.FromSuccess();
        }
    }
}