using System;
using Octopus.CoreUtilities;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureScripting;
using Sashimi.AzureServiceFabric.Endpoints;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureServiceFabric
{
    class AzureServiceFabricAppActionHandler : IActionHandler
    {
        public string Id => SpecialVariables.Action.ServiceFabric.ServiceFabricAppActionTypeName;
        public string Name => "Deploy a Service Fabric App";
        public string Description => "Deploy the contents of a package to a Service Fabric cluster (Azure or on-prem).";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, ActionHandlerCategory.Azure, ActionHandlerCategory.Package };

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            var isLegacyAction = !string.IsNullOrWhiteSpace(SpecialVariables.Action.ServiceFabric.ConnectionEndpoint);
            // ReSharper disable once InvertIf
            if (!isLegacyAction && context.DeploymentTargetType.Some())
            {
                if (context.DeploymentTargetType.Value != AzureServiceFabricClusterEndpoint.AzureServiceFabricClusterDeploymentTargetType)
                    throw new ControlledActionFailedException($"The machine {context.DeploymentTargetName.SomeOr("<unknown>")} will not be deployed to because it is not an {AzureServiceFabricClusterEndpoint.AzureServiceFabricClusterDeploymentTargetType.DisplayName} target.");
            }

            return context.CalamariCommand(CalamariFlavours.CalamariServiceFabric, "deploy-azure-service-fabric-app")
                .WithAzureTools(context, taskLog)
                .WithStagedPackageArgument()
                .Execute(taskLog);
        }
    }
}