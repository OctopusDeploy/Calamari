using System;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.GCPScripting
{
    class GoogleCloudActionHandler : IActionHandlerWithAccount
    {
        public string Id => SpecialVariables.Action.GoogleCloud.ActionTypeName;
        public string Name => "Run a Google Cloud Script";
        public string Description => "Runs a custom script using a Google Cloud CLI.";
        public string? Keywords => null;
        public bool ShowInStepTemplatePickerUI => true;
        public bool WhenInAChildStepRunInTheContextOfTheTargetMachine => false;
        public bool CanRunOnDeploymentTarget => false;
        public ActionHandlerCategory[] Categories => new[] { ActionHandlerCategory.BuiltInStep, GoogleCloudConstants.GoogleCloudActionHandlerCategory, ActionHandlerCategory.Script };
        public string[] StepBasedVariableNameForAccountIds { get; } = {SpecialVariables.Action.GoogleCloud.AccountId};

        public IActionHandlerResult Execute(IActionHandlerContext context, ITaskLog taskLog)
        {
            var builder = context.CalamariCommand(GoogleCloudConstants.CalamariAzure, "run-script");

            var isInPackage = KnownVariableValues.Action.Script.ScriptSource.Package.Equals(context.Variables.Get(KnownVariables.Action.Script.ScriptSource), StringComparison.OrdinalIgnoreCase);
            if (isInPackage)
            {
                builder.WithStagedPackageArgument();
            }

            return builder.Execute(taskLog);
        }
    }
}