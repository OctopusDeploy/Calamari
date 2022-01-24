using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Calamari.AzureAppService.Azure
{
    public class TargetDiscoveryContext
    {
        [JsonConstructor]
        public TargetDiscoveryContext(TargetDiscoveryScope scope, TargetDiscoveryAuthentication authentication)
        {
            this.Scope = scope;
            this.Authentication = authentication;
        }

        public TargetDiscoveryScope Scope { get; }

        public TargetDiscoveryAuthentication Authentication { get; }

        public class TargetDiscoveryScope
        {
            [JsonConstructor]
            public TargetDiscoveryScope(
                string spaceId,
                string environmentId,
                string projectId,
                string tenantId,
                IEnumerable<string> roles,
                string workerPoolId)
            {
                this.SpaceId = spaceId;
                this.EnvironmentId = environmentId;
                this.ProjectId = projectId;
                this.TenantId = tenantId;
                this.Roles = roles;
                this.WorkerPoolId = workerPoolId;
            }

            public string SpaceId { get; }

            public string EnvironmentId { get; }

            public string ProjectId { get; }

            public string TenantId { get; }

            public IEnumerable<string> Roles { get; }

            public string WorkerPoolId { get; }
        }

        public class TargetDiscoveryAuthentication
        {
            public TargetDiscoveryAuthentication(
                string accountId,
                AzureAccountDetails account)
            {
                this.AccountId = accountId;
                this.Account = account;
            }

            public string AccountId { get; }

            public AzureAccountDetails Account { get; }
        }

        public class AzureAccountDetails
        {
            public AzureAccountDetails(
                string subscriptionNumber,
                string clientId,
                string tenantId,
                string password,
                string azureEnvironment,
                string resourceManagementEndpointBaseUri,
                string activeDirectoryEndpointBaseUri)
            {
                this.SubscriptionNumber = subscriptionNumber;
                this.ClientId = clientId;
                this.TenantId = tenantId;
                this.Password = password;
                this.AzureEnvironment = azureEnvironment;
                this.ResourceManagementEndpointBaseUri = resourceManagementEndpointBaseUri;
                this.ActiveDirectoryEndpointBaseUri = activeDirectoryEndpointBaseUri;
            }

            public string SubscriptionNumber { get; }       
            public string ClientId { get; }
            public string TenantId { get; }
            public string Password { get; }
            public string AzureEnvironment { get; }
            public string ResourceManagementEndpointBaseUri { get; }
            public string ActiveDirectoryEndpointBaseUri { get; }
        }
    }
}
