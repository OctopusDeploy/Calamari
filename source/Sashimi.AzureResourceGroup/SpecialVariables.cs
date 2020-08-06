namespace Sashimi.AzureResourceGroup
{
    static class SpecialVariables
    {
        public static readonly string AccountType = "Octopus.Account.AccountType";

        public static class Action
        {
            public static class Azure
            {
                public static readonly string WebAppName = "Octopus.Action.Azure.WebAppName";
                public static readonly string WebAppSlot = "Octopus.Action.Azure.DeploymentSlot";
                public static readonly string ResourceGroupName = "Octopus.Action.Azure.ResourceGroupName";
                public static readonly string AccountId = "Octopus.Action.Azure.AccountId";
                public static readonly string WebAppHealthCheckActionTypeName = "Octopus.HealthCheck.AzureWebApp";
                public static readonly string WebAppActionTypeName = "Octopus.AzureWebApp";
                public static readonly string UseBundledAzureModules = "OctopusUseBundledAzureModules";
                public static readonly string UseBundledAzureCLI = "OctopusUseBundledAzureCLI";
                public static readonly string OctopusDisableAzureCLI = "OctopusDisableAzureCLI";
                public static readonly string UseBundledAzureModulesLegacy = "Octopus.Action.Azure.UseBundledAzurePowerShellModules";
                public static readonly string IsLegacyMode = "Octopus.Action.Azure.IsLegacyMode";
            }

            public static class AzureResourceGroup
            {
                public static readonly string ResourceGroupActionTypeName = "Octopus.AzureResourceGroup";

                public static readonly string ResourceGroupDeploymentMode = "Octopus.Action.Azure.ResourceGroupDeploymentMode";

                public static readonly string TemplateSource = "Octopus.Action.Azure.TemplateSource";

                public static readonly string ResourceGroupTemplate = "Octopus.Action.Azure.ResourceGroupTemplate";

                public static readonly string ResourceGroupTemplateParameters = "Octopus.Action.Azure.ResourceGroupTemplateParameters";
            }
        }
    }
}