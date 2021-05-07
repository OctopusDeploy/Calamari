using Octopus.CoreUtilities;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.AzureScripting;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureAppService
{
    /*
    class AzureAppSettingsActionHandler : IActionHandler
    {
        private const string AzureWebAppDeploymentTargetTypeId = "AzureWebApp";
        public string Id => SpecialVariables.Action.Azure.ActionTypeName;
        public string Name => "Deploy app settings/connection strings to an Azure App Service";
        public string Description => "Deploy app settings and connection strings to an existing app service";
        public string? Keywords => "Azure";
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;

        public ActionHandlerCategory[] Categories => new[]
            {ActionHandlerCategory.BuiltInStep, AzureConstants.AzureActionHandlerCategory};

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            if (context.DeploymentTargetType.Some())
            {
                if (context.DeploymentTargetType.Value.Id != AzureWebAppDeploymentTargetTypeId)
                {
                    throw new ControlledActionFailedException(
                        $"The machine {context.DeploymentTargetName.SomeOr("<unknown>")} will not be deployed to because it is not an Azure Web Application deployment target");
                }
            }
            
            return context.CalamariCommand(AzureConstants.CalamariAzure, "deploy-azure-app-settings").WithAzureTools(context, taskLog)
                .WithStagedPackageArgument().Execute(taskLog);
        }
    }
    */
}
