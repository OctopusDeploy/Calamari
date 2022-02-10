using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureScripting;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.AzureAppService
{
    class AzureWebAppDiscoveryActionHandler : IActionHandler
    {
        static readonly CalamariFlavour CalamariAzureAppService = new CalamariFlavour("Calamari.AzureAppService");

        public string Id => SpecialVariables.Action.Azure.WebAppDiscoveryActionTypeName;
        public string Name => "Discover Azure Web Apps";
        public string Description => "Discover available Azure Web Apps.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => false;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, ActionHandlerCategory.Azure };

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            return context.CalamariCommand(CalamariAzureAppService, "target-discovery")
              .Execute(taskLog);
        }
    }
}