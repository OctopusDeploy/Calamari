namespace Calamari.Azure
{
    static class SpecialVariables
    {
        public static class Action
        {
            public static class Azure
            {
                /// <summary>
                /// Expected Values:
                /// ZipDeploy,
                /// ImageDeploy,
                /// DockerCompose
                /// </summary>
                public static readonly string DeploymentType = "Octopus.Action.Azure.DeploymentType";
                public static readonly string ResourceGroupName = "Octopus.Action.Azure.ResourceGroupName";
                public static readonly string WebAppName = "Octopus.Action.Azure.WebAppName";
                public static readonly string WebAppSlot = "Octopus.Action.Azure.DeploymentSlot";

                public static readonly string AppSettings = "Octopus.Action.Azure.AppSettings";
                public static readonly string ContainerInitTimeout = "Octopus.Action.Azure.ContainerInitTimeout";
            }

            public static class Package
            {
                public static readonly string FeedId = "Octopus.Action.Package.FeedId";
                public static readonly string PackageId = "Octopus.Action.Package.PackageId";
                public static readonly string PackageVersion = "Octopus.Action.Package.PackageVersion";
            }

        }
    }
}