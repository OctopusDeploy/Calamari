using Octopus.Diagnostics;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CommandBuilders;

namespace Sashimi.Aws.ActionHandler
{
    static class AwsActionHandlerExtensions
    {
        public static ICalamariCommandBuilder WithAwsTools(
            this ICalamariCommandBuilder builder,
            IActionHandlerContext context,
            ILog log)
        {
            // This is the new value that the user can set on the step. It and the legacy variable both default to true, if either are false then
            // we don't include the tooling.
            var useBundledTooling = context.Variables.GetFlag(KnownVariables.Action.UseBundledTooling, true);

            var legacyModuleBundling = context.Variables.GetFlag(AwsSpecialVariables.Action.Aws.UseBundledAwsPowerShellModules, true);

            if (legacyModuleBundling == false)
            {
                // user has explicitly used the legacy flag to switch off bundling, tell them it's available on the step now
                log.Warn($"The {AwsSpecialVariables.Action.Aws.UseBundledAwsPowerShellModules} variable has been used to disable using the bundled AWS PowerShell modules. Note that this variable is deprecated and will be removed in a future version, please use the bundling options on the step to control this behavior now.");
            }

            if (useBundledTooling && legacyModuleBundling)
                builder = builder.WithTool(AwsTools.AwsPowershell);

            var legacyCliBundling = context.Variables.GetFlag(AwsSpecialVariables.Action.Aws.UseBundledAwsCLI,true);

            if (legacyCliBundling == false)
            {
                // user has explicitly used the legacy flag to switch off bundling, tell them it's available on the step now
                log.Warn($"The {AwsSpecialVariables.Action.Aws.UseBundledAwsCLI} variable has been used to disable using the bundled AWS CLI. Note that this variable is deprecated and will be removed in a future version, please use the bundling options on the step to control this behavior now.");
            }

            if (useBundledTooling && legacyCliBundling)
                builder = builder.WithTool(AwsTools.AwsCli);

            return builder;
        }
    }
}