using System;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Azure
{
    public interface IAzureAccount
    {
        string SubscriptionNumber { get;  }
        string ClientId { get; }
        string TenantId { get; }
        string AzureEnvironment { get; }
        string ResourceManagementEndpointBaseUri { get; }
        string ActiveDirectoryEndpointBaseUri { get; }

        public abstract AccountType AccountType { get;  }
        public abstract string GetCredential { get; }
    }

    public enum AccountType
    {
        AzureServicePrincipal,
        AzureOidc
    }
}
