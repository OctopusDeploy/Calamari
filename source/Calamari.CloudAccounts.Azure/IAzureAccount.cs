using System;

namespace Calamari.CloudAccounts.Azure
{
    public interface IAzureAccount
    {
        string SubscriptionNumber { get;  }
        string ClientId { get; }
        string TenantId { get; }
        string AzureEnvironment { get; }
        string ResourceManagementEndpointBaseUri { get; }
        string ActiveDirectoryEndpointBaseUri { get; }

        AccountType AccountType { get; }
        string GetCredentials { get; }
    }

    public enum AccountType
    {
        AzureServicePrincipal,
        AzureOidc
    }
}