using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using AzureEnvironmentEnum = Microsoft.Azure.Management.ResourceManager.Fluent.AzureEnvironment;

namespace Calamari.Azure
{
    public interface IAzureAccount
    {
        string SubscriptionNumber { get; }
        string ClientId { get; }
        string TenantId { get; }
        string AzureEnvironment { get; }
        string ResourceManagementEndpointBaseUri { get; }
        string ActiveDirectoryEndpointBaseUri { get; }

        string GetCredentials();
        IAzure CreateAzureClient();
    }
}
