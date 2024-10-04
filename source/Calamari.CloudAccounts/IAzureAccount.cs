using System;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Logging;

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

        Task<string> GetAuthorizationToken(ILog log, CancellationToken cancellationToken);
    }

    public enum AccountType
    {
        AzureServicePrincipal,
        AzureOidc
    }
}
