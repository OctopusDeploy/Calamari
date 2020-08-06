using System;
using Sashimi.Azure.Accounts;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CommandBuilders;

namespace Sashimi.AzureScripting
{
    public static class AzureActionHandlerExtensions
    {
        public static ICalamariCommandBuilder WithAzureTools(
            this ICalamariCommandBuilder builder,
            IActionHandlerContext context)
        {
            return builder.WithAzureCmdlets(context).WithAzureCLI(context);
        }

        public static ICalamariCommandBuilder WithCheckAccountIsNotManagementCertificate(this ICalamariCommandBuilder builder, IActionHandlerContext context)
        {
            if (context.Variables.Get(SpecialVariables.AccountType) != AccountTypes.AzureServicePrincipalAccountType.ToString())
            {
                context.Log.Warn("Azure have announced they will be retiring Service Management API support on June 30th 2018. Please switch to using Service Principals for your Octopus Azure accounts https://g.octopushq.com/AzureServicePrincipalAccount");
            }

            return builder;
        }

        static ICalamariCommandBuilder WithAzureCmdlets(
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

        static ICalamariCommandBuilder WithAzureCLI(
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
