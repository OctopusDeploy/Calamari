namespace Calamari.CloudAccounts
{
    public static class KnownVariables
    {
        public static class Azure
        {
            public static readonly string Environment = "Octopus.Action.Azure.Environment";
            public static readonly string AccountVariable = "Octopus.Action.AzureAccount.Variable";
            public static readonly string SubscriptionId = "Octopus.Action.Azure.SubscriptionId";
            public static readonly string ClientId = "Octopus.Action.Azure.ClientId";   
            public static readonly string TenantId = "Octopus.Action.Azure.TenantId";
            public static readonly string Password = "Octopus.Action.Azure.Password";
        }
    }
}