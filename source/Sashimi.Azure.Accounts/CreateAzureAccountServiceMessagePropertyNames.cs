namespace Sashimi.Azure.Accounts
{
    static class CreateAzureAccountServiceMessagePropertyNames
    {
        public const string Name = "create-azureaccount";

        public const string SubscriptionAttribute = "azSubscriptionId";
        public static class ServicePrincipal
        {
            public const string ApplicationAttribute = "azApplicationId";
            public const string TenantAttribute = "azTenantId";
            public const string PasswordAttribute = "azPassword";
            public const string EnvironmentAttribute = "azEnvironment";
            public const string BaseUriAttribute = "azBaseUri";
            public const string ResourceManagementBaseUriAttribute = "azResourceManagementBaseUri";
        }
    }
}