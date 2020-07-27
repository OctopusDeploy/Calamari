using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CommandBuilders;

namespace Sashimi.AzureWebApp
{
    public interface IAzurePowerShellModuleConfiguration
    {
        string AzurePowerShellModule { get; set; }
    }

    public class AzurePowerShellModuleConfiguration : IAzurePowerShellModuleConfiguration
    {
        readonly IKeyValueStore settings;
        const string Key = "Octopus.Azure.PowerShellModule";

        public AzurePowerShellModuleConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public string AzurePowerShellModule
        {
            get { return settings.Get<string>(Key); }
            set { settings.Set(Key, value); }
        }
    }

    public static class AzureActionHandlerExtensions
    {
        public static ICalamariCommandBuilder WithAzurePowershellConfiguration(
            this ICalamariCommandBuilder builder,
            IAzurePowerShellModuleConfiguration azurePowerShellModuleConfiguration
        )
        {
            var configuredPowerShellModulePath = azurePowerShellModuleConfiguration.AzurePowerShellModule;
            if (!string.IsNullOrWhiteSpace(configuredPowerShellModulePath))
                builder.WithVariable(SpecialVariables.Action.Azure.PowershellModulePath, configuredPowerShellModulePath);
            return builder;
        }

        public static ICalamariCommandBuilder WithAzureTools(
            this ICalamariCommandBuilder builder,
            IActionHandlerContext context)
        {
            return builder.WithAzureCmdlets(context).WithAzureCLI(context);
        }

        public static ICalamariCommandBuilder WithAzureCmdlets(
            this ICalamariCommandBuilder builder,
            IActionHandlerContext context)
        {
            // This is the new value that the user can set on the step. It and the legacy variable both default to true, if either are false then
            // we don't include the tooling.
            var useBundledTooling = context.Variables.GetFlag(KnownVariables.Action.UseBundledTooling, true);

            var legacyModuleBundling = context.Variables.GetFlag(SpecialVariables.Action.Azure.UseBundledAzureModules, context.Variables.GetFlag(SpecialVariables.Action.Azure.UseBundledAzureModulesLegacy, true));

            if (legacyModuleBundling == false)
            {
                // user has explicitly used the legacy flag to switch off bundling, tell them it's available on the step now
                context.Log.Warn($"The {SpecialVariables.Action.Azure.UseBundledAzureModules} variable has been used to disable using the bundled Azure PowerShell modules. Note that this variable is deprecated and will be removed in a future version, please use the bundling options on the step to control this behavior now.");
            }

            if (useBundledTooling && legacyModuleBundling)
                builder = builder.WithTool(AzureTools.AzureCmdlets);

            return builder;
        }

        public static ICalamariCommandBuilder WithAzureCLI(
            this ICalamariCommandBuilder builder,
            IActionHandlerContext context)
        {
            // This is the new value that the user can set on the step. It and the legacy variable both default to true, if either are false then
            // we don't include the tooling.
            var useBundledTooling = context.Variables.GetFlag(KnownVariables.Action.UseBundledTooling, true);

            var legacyCliBundling = context.Variables.GetFlag(SpecialVariables.Action.Azure.UseBundledAzureCLI, true);

            if (legacyCliBundling == false)
            {
                // user has explicitly used the legacy flag to switch off bundling, tell them it's available on the step now
                context.Log.Warn($"The {SpecialVariables.Action.Azure.UseBundledAzureCLI} variable has been used to disable using the bundled Azure CLI. Note that this variable is deprecated and will be removed in a future version, please use the bundling options on the step to control this behavior now.");
            }

            if (useBundledTooling && legacyCliBundling)
                builder = builder.WithTool(AzureTools.AzureCLI);

            return builder;
        }
    }
}
