namespace Calamari.Azure
{
    public static class AzureVariables
    {
        public static class Action
        {
            public static class Azure
            {
                /// <summary>
                /// Expected Values:
                /// Package,
                /// Container,
                /// </summary>
                public static readonly string DeploymentType = "Octopus.Action.Azure.DeploymentType";

                public static readonly string ResourceGroupName = "Octopus.Action.Azure.ResourceGroupName";
                public static readonly string WebAppName = "Octopus.Action.Azure.WebAppName";
                public static readonly string WebAppSlot = "Octopus.Action.Azure.DeploymentSlot";
                public static readonly string AppSettings = "Octopus.Action.Azure.AppSettings";
                public static readonly string ConnectionStrings = "Octopus.Action.Azure.ConnectionStrings";
                public static readonly string AsyncZipDeploymentTimeout = "Octopus.Action.Azure.AsyncZipDeploymentTimeout";
            }
        }
    }
}