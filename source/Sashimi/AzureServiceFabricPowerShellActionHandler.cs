using System;
using Octopus.CoreUtilities;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureScripting;
using Sashimi.AzureServiceFabric.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureServiceFabric
{
    class AzureServiceFabricPowerShellActionHandler : IActionHandler
    {
        public string Id => SpecialVariables.Action.ServiceFabric.ServiceFabricPowerShellActionTypeName;
        public string Name => "Run a Service Fabric SDK PowerShell Script";
        public string Description => "Runs PowerShell using a Service Fabric cluster context (Azure or on-prem).";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, ActionHandlerCategory.Azure, ActionHandlerCategory.Script };

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            var isLegacyAction = !string.IsNullOrWhiteSpace(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint);

            if (!isLegacyAction && context.DeploymentTargetType.Some())
            {
                if (context.DeploymentTargetType.Value != AzureServiceFabricClusterEndpoint.AzureServiceFabricClusterDeploymentTargetType)
                    throw new ControlledActionFailedException($"The machine {context.DeploymentTargetName.SomeOr("<unknown>")} will not be deployed to because it is not an {AzureServiceFabricClusterEndpoint.AzureServiceFabricClusterDeploymentTargetType.DisplayName} target.");
            }

            var builder = context.CalamariCommand(CalamariFlavours.CalamariServiceFabric, "run-script")
                                 .WithAzureTools(context, taskLog);

            var isInPackage = KnownVariableValues.Action.Script.ScriptSource.Package.Equals(context.Variables.Get(KnownVariables.Action.Script.ScriptSource), StringComparison.OrdinalIgnoreCase);
            if (isInPackage)
            {
                builder.WithStagedPackageArgument();
            }

            return builder.Execute(taskLog);
        }
    }
}