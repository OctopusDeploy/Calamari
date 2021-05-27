using System;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.GCPScripting
{
    class GoogleCloudActionHandler : IActionHandler
    {
        public string Id => SpecialVariables.Action.GoogleCloud.ActionTypeName;
        public string Name => "Run gcloud in a Script";
        public string Description => "Run gcloud commands in a custom script";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, GoogleCloudConstants.GoogleCloudActionHandlerCategory, ActionHandlerCategory.Script };

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            var builder = context.CalamariCommand(GoogleCloudConstants.CalamariGoogleCloud, "run-script");

            var isInPackage = KnownVariableValues.Action.Script.ScriptSource.Package.Equals(context.Variables.Get(KnownVariables.Action.Script.ScriptSource), StringComparison.OrdinalIgnoreCase);
            if (isInPackage)
            {
                builder.WithStagedPackageArgument();
            }

            return builder.Execute(taskLog);
        }
    }
}