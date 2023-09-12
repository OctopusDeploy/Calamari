using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;

namespace Calamari.CloudAccounts
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
        IAzure CreateAzureClient();
    }

    public enum AccountType
    {
        AzureServicePrincipal,
        AzureOidc
    }
}
