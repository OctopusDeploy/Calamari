using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;

namespace Sashimi.AzureServiceFabric
{
    class AzureServiceFabricAppHealthCheckActionHandler : IActionHandlerWithAccount
    {
        static readonly CalamariFlavour CalamariServiceFabric = new CalamariFlavour("Calamari.AzureServiceFabric");

        public string Id => SpecialVariables.Action.ServiceFabric.AppHealthCheckActionTypeName;
        public string Name => "HealthCheck an Azure ServiceFabric cluster";
        public string Description => "HealthCheck an Azure ServiceFabric cluster.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => false;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, ActionHandlerCategory.Azure };
        public string[] StepBasedVariableNameForAccountIds { get; } = {SpecialVariables.Action.AccountId};

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            return context.CalamariCommand(CalamariServiceFabric, "health-check")
                .Execute(taskLog);
        }
    }
}