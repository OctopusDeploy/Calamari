namespace Calamari.Azure
{
    static class SpecialVariables
    {
        public static class Action
        {
            public static class Azure
            {
                /// <summary>
                /// deployment type should be either 'zipdeploy' or 'container'
                /// </summary>
                public static readonly string DeploymentType = "Octopus.Action.Azure.DeploymentType";
                public static readonly string ResourceGroupName = "Octopus.Action.Azure.ResourceGroupName";
                public static readonly string WebAppName = "Octopus.Action.Azure.WebAppName";
                public static readonly string WebAppSlot = "Octopus.Action.Azure.DeploymentSlot";

                public static readonly string AppSettings = "Octopus.Action.Azure.AppSettings";

                /// <summary>
                /// json string of docker settings:
                /// Registry URL:(Default: Docker: https://index.docker.io)
                ///
                /// </summary>
                public static readonly string ContainerSettings = "Octopus.Action.Azure.ContainerSettings";
            }
        }
    }
}