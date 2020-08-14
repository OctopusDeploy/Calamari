using System;

namespace Sashimi.AzureScripting
{
    static class SpecialVariables
    {
        public static readonly string AccountType = "Octopus.Account.AccountType";

        public static class Action
        {
            public static class Azure
            {
                public static readonly string ActionTypeName = "Octopus.AzurePowerShell";
                public static readonly string UseBundledAzureModules = "OctopusUseBundledAzureModules";
                public static readonly string UseBundledAzureCLI = "OctopusUseBundledAzureCLI";
                public static readonly string UseBundledAzureModulesLegacy = "Octopus.Action.Azure.UseBundledAzurePowerShellModules";
                public static readonly string AccountId = "Octopus.Action.Azure.AccountId";
            }
        }
    }
}