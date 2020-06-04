namespace Sashimi.AzureCloudService
{
    class SpecialVariables
    {
        public static class Action
        {
            public static class Azure
            {
                public static readonly string SubscriptionId = "Octopus.Action.Azure.SubscriptionId";
                public static readonly string CertificateThumbprint = "Octopus.Action.Azure.CertificateThumbprint";
                public static readonly string CertificateBytes = "Octopus.Action.Azure.CertificateBytes";
                public static readonly string Environment = "Octopus.Action.Azure.Environment";
                public static readonly string ServiceManagementEndPoint = "Octopus.Action.Azure.ServiceManagementEndPoint";
                public static readonly string StorageEndPointSuffix = "Octopus.Action.Azure.StorageEndpointSuffix";
            }
        }
    }
}