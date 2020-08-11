namespace Sashimi.AzureResourceGroup
{
    static class SpecialVariables
    {
        public static class Action
        {
            public static class Azure
            {
                public static readonly string ResourceGroupName = "Octopus.Action.Azure.ResourceGroupName";
                public static readonly string AccountId = "Octopus.Action.Azure.AccountId";
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