using System;
using System.Collections.Generic;
using System.Text;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Azure
{
    class ServicePrincipalAccount
    {
        public ServicePrincipalAccount(IVariables variables)
        {
            SubscriptionNumber = variables.Get(AccountVariables.SubscriptionId);
            ClientId = variables.Get(AccountVariables.ClientId);
            TenantId = variables.Get(AccountVariables.TenantId);
            Password = variables.Get(AccountVariables.Password);

            AzureEnvironment = variables.Get(AccountVariables.Environment);
            ResourceManagementEndpointBaseUri = variables.Get(AccountVariables.ResourceManagementEndPoint, DefaultVariables.ResourceManagementEndpoint);
            ActiveDirectoryEndpointBaseUri = variables.Get(AccountVariables.ActiveDirectoryEndPoint, DefaultVariables.ActiveDirectoryEndpoint);
        }
        
        public string SubscriptionNumber { get; set; }

        public string ClientId { get; set; }

        public string TenantId { get; set; }

        public string Password { get; set; }

        public string AzureEnvironment { get; set; }
        public string ResourceManagementEndpointBaseUri { get; set; }
        public string ActiveDirectoryEndpointBaseUri { get; set; }
    }
}
