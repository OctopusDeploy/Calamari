using System;
using Azure.Core;
using Azure.Identity;
using Calamari.Common.Plumbing.Variables;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Azure
{
    class ServicePrincipalAccount
    {
        [JsonConstructor]
        public ServicePrincipalAccount(
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

        public static ServicePrincipalAccount CreateFromKnownVariables(IVariables variables) =>
            new ServicePrincipalAccount(
                                        subscriptionNumber: variables.Get(AccountVariables.SubscriptionId),
                                        clientId: variables.Get(AccountVariables.ClientId),
                                        tenantId: variables.Get(AccountVariables.TenantId),
                                        password: variables.Get(AccountVariables.Password),
                                        azureEnvironment: variables.Get(AccountVariables.Environment),
                                        resourceManagementEndpointBaseUri: variables.Get(AccountVariables.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint),
                                        activeDirectoryEndpointBaseUri: variables.Get(AccountVariables.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint));

        public string SubscriptionNumber { get; }

        public string ClientId { get; }

        public string TenantId { get; }

        public string Password { get; }

        public string AzureEnvironment { get; }

        public string ResourceManagementEndpointBaseUri { get; }

        public string ActiveDirectoryEndpointBaseUri { get; }
    }
}