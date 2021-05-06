using Octopus.CoreUtilities;
using Sashimi.AzureScripting;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureAppService
{
    class AzureAppServiceActionHandler : IActionHandler
    {
        const string AzureWebAppDeploymentTargetTypeId = "AzureWebApp";

        public string Id => SpecialVariables.Action.Azure.ActionTypeName;

        public string Name => "Deploy an Azure App Service";

        public string Description => "Deploy a package or container image to an Azure App Service (Azure Web App)";

        public string? Keywords => null;

        public bool ShowInStepTemplatePickerUI => true;

        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;

        public bool CanRunOnDeploymentTarget => false;

        public ActionHandlerCategory[] Categories => new[]
            {ActionHandlerCategory.BuiltInStep, AzureConstants.AzureActionHandlerCategory};

        public IActionHandlerResult Execute(IActionHandlerContext context)
        {
            if (context.DeploymentTargetType.Some())
            {
                if (context.DeploymentTargetType.Value.Id != AzureWebAppDeploymentTargetTypeId)
                    throw new ControlledActionFailedException(
                        $"The machine {context.DeploymentTargetName.SomeOr("<unknown>")} will not be deployed to because it is not an Azure Web Application deployment target");
            }

            var commandBuilder = context.CalamariCommand(AzureConstants.CalamariAzure, "deploy-azure-app-service")
                .WithAzureTools(context);

            // If we are deploying a container image, then there won't be a staged package
            if (!context.Variables.Get(SpecialVariables.Action.Azure.DeploymentType, "").Equals("Container"))
            {
                commandBuilder = commandBuilder.WithStagedPackageArgument();
            }

            return commandBuilder.Execute();
        }
    }
}
