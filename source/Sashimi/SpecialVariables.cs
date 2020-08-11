namespace Sashimi.AzureCloudService
{
    //TODO: This is duplicated from Server while we sort out a way for Sashimi to contribute special variables.
    static class SpecialVariables
    {
        public static readonly string AccountType = "Octopus.Account.AccountType";

        public static class Account
        {
            public static readonly string Id = "Octopus.Account.Id";
        }

        public static class Action
        {
            public static class Azure
            {
                public static readonly string CloudServiceActionTypeName = "Octopus.AzureCloudService";

                public static readonly string CloudServiceName = "Octopus.Action.Azure.CloudServiceName";
                public static readonly string StorageAccountName = "Octopus.Action.Azure.StorageAccountName";
                public static readonly string Slot = "Octopus.Action.Azure.Slot";
                public static readonly string SwapIfPossible = "Octopus.Action.Azure.SwapIfPossible";
                public static readonly string UseCurrentInstanceCount = "Octopus.Action.Azure.UseCurrentInstanceCount";

                public static readonly string SubscriptionId = "Octopus.Action.Azure.SubscriptionId";
                public static readonly string CertificateThumbprint = "Octopus.Action.Azure.CertificateThumbprint";
                public static readonly string CertificateBytes = "Octopus.Action.Azure.CertificateBytes";
                public static readonly string Environment = "Octopus.Action.Azure.Environment";
                public static readonly string ServiceManagementEndPoint = "Octopus.Action.Azure.ServiceManagementEndPoint";
                public static readonly string StorageEndPointSuffix = "Octopus.Action.Azure.StorageEndpointSuffix";
                public static readonly string CloudServiceHealthCheckActionTypeName = "Octopus.HealthCheck.AzureCloudService";
                public static readonly string AccountId = "Octopus.Action.Azure.AccountId";

                public static readonly string IsLegacyMode = "Octopus.Action.Azure.IsLegacyMode";
            }
        }
    }
}