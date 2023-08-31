using System;

namespace Calamari.AzureWebApp
{
    public interface IAzureAccount
    {
        string SubscriptionNumber { get; }
        string ClientId { get; }
        string TenantId { get; }
        string AzureEnvironment { get; }
        string ResourceManagementEndpointBaseUri { get; }
        string ActiveDirectoryEndpointBaseUri { get; }

        string GetCredentials { get; }
        AccountType AccountType { get; }
    }

    public enum AccountType
    {
        AzureServicePrincipal,
        AzureOidc
    }
}
