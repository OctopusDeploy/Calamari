namespace Sashimi.AzureWebApp
{
    //TODO: This is duplicated from Server while we sort out a way for Sashimi to contribute special variables.
    static class SpecialVariables
    {
        public static class Action
        {
            public static class Azure
            {

                public static readonly string WebAppName = "Octopus.Action.Azure.WebAppName";

                public static readonly string WebAppSlot = "Octopus.Action.Azure.DeploymentSlot";

                public static readonly string ResourceGroupName = "Octopus.Action.Azure.ResourceGroupName";
            }
        }
    }
}