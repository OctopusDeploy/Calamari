using System.Collections.Generic;
using Octopus.Data.Model;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Azure.Accounts
{
    class AzureServicePrincipalAccountServiceMessageHandler : ICreateAccountDetailsServiceMessageHandler
    {
        public string AuditEntryDescription => "Azure Service Principal account";
        public string ServiceMessageName => CreateAzureAccountServiceMessagePropertyNames.Name;

        public AccountDetails CreateAccountDetails(IDictionary<string, string> properties)
        {
            properties.TryGetValue(CreateAzureAccountServiceMessagePropertyNames.SubscriptionAttribute,
                                   out var subscriptionNumber);
            properties.TryGetValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ApplicationAttribute,
                                   out var clientId);
            properties.TryGetValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.PasswordAttribute,
                                   out var password);
            properties.TryGetValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.TenantAttribute,
                                   out var tenantId);

            var details = new AzureServicePrincipalAccountDetails
            {
                SubscriptionNumber = subscriptionNumber,
                ClientId = clientId,
                Password = password?.ToSensitiveString(),
                TenantId = tenantId
            };

            if (properties.TryGetValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute, out var environment) &&
                !string.IsNullOrWhiteSpace(environment))
            {
                properties.TryGetValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.BaseUriAttribute,
                                       out var baseUri);
                properties.TryGetValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ResourceManagementBaseUriAttribute,
                                       out var resourceManagementBaseUri);

                details.AzureEnvironment = environment;
                details.ActiveDirectoryEndpointBaseUri = baseUri;
                details.ResourceManagementEndpointBaseUri = resourceManagementBaseUri;
            }
            else
            {
                details.AzureEnvironment = string.Empty;
                details.ActiveDirectoryEndpointBaseUri = string.Empty;
                details.ResourceManagementEndpointBaseUri = string.Empty;
            }

            return details;
        }

        internal static class CreateAzureAccountServiceMessagePropertyNames
        {
            public const string Name = "create-azureaccount";

            public const string SubscriptionAttribute = "azSubscriptionId";
            public static class ServicePrincipal
            {
                public const string ApplicationAttribute = "azApplicationId";
                public const string TenantAttribute = "azTenantId";
                public const string PasswordAttribute = "azPassword";
                public const string EnvironmentAttribute = "azEnvironment";
                public const string BaseUriAttribute = "azBaseUri";
                public const string ResourceManagementBaseUriAttribute = "azResourceManagementBaseUri";
            }
        }
    }
}