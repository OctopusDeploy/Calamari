
namespace Sashimi.AzureAppService
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
                public static readonly string ActionTypeName = "Octopus.AzureAppService";
                public static readonly string AppSettings = "Octopus.Action.Azure.AppSettings";
                public static readonly string DeploymentType = "Octopus.Action.Azure.DeploymentType";
            }
        }
    }
}
