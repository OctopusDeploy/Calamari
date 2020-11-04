
namespace Sashimi.AzureAppService
{
    static class SpecialVariables
    {
        public static readonly string AccountType = "Octopus.Account.AccountType";

        public static class Action
        {
            public static class Azure
            {
                public static readonly string WebAppSlot = "Octopus.Action.Azure.DeploymentSlot";
                public static readonly string WebAppActionTypeName = "Octopus.AzureAppService";
            }
        }
    }
}
