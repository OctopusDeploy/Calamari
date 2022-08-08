namespace Calamari.AzureResourceGroup
{
    static class SpecialVariables
    {
        public static class Action
        {
            public static class Azure
            {
                public static readonly string ResourceGroupTemplate = "Octopus.Action.Azure.ResourceGroupTemplate";
                public static readonly string ResourceGroupTemplateParameters = "Octopus.Action.Azure.ResourceGroupTemplateParameters";
                public static readonly string ResourceGroupName = "Octopus.Action.Azure.ResourceGroupName";
                public static readonly string ResourceGroupDeploymentName = "Octopus.Action.Azure.ResourceGroupDeploymentName";
                public static readonly string ResourceGroupDeploymentMode = "Octopus.Action.Azure.ResourceGroupDeploymentMode";
                public static readonly string Template = "Octopus.Action.Azure.Template";
                public static readonly string TemplateParameters = "Octopus.Action.Azure.TemplateParameters";
                public static readonly string TemplateSource = "Octopus.Action.Azure.TemplateSource";
            }
        }
    }
}