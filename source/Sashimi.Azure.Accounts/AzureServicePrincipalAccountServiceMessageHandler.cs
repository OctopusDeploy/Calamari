using System.Collections.Generic;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Azure.Accounts
{
    class AzureServicePrincipalAccountServiceMessageHandler : ICreateAccountDetailsServiceMessageHandler
    {
        public string AuditEntryDescription => "Azure Service Principal account";
        public string ServiceMessageName => CreateAzureAccountServiceMessagePropertyNames.CreateAccountName;
        public IEnumerable<ScriptFunctionRegistration> ScriptFunctionRegistrations { get; } = new List<ScriptFunctionRegistration>
        {
            new ScriptFunctionRegistration("OctopusAzureServicePrincipalAccount",
                                           "Creates a new Azure Service Principal Account.",
                                           CreateAzureAccountServiceMessagePropertyNames.CreateAccountName,
                                           new Dictionary<string, FunctionParameter>
                                           {
                                               { CreateAzureAccountServiceMessagePropertyNames.NameAttribute, new FunctionParameter(ParameterType.String) },
                                               { CreateAzureAccountServiceMessagePropertyNames.SubscriptionAttribute, new FunctionParameter(ParameterType.String) },
                                               { CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ApplicationAttribute, new FunctionParameter(ParameterType.String) },
                                               { CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.TenantAttribute, new FunctionParameter(ParameterType.String) },
                                               { CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.PasswordAttribute, new FunctionParameter(ParameterType.String) },
                                               { CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute, new FunctionParameter(ParameterType.String, CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute) },
                                               { CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.BaseUriAttribute, new FunctionParameter(ParameterType.String, CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute) },
                                               { CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ResourceManagementBaseUriAttribute, new FunctionParameter(ParameterType.String, CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute) },
                                               { CreateAzureAccountServiceMessagePropertyNames.UpdateIfExistingAttribute, new FunctionParameter(ParameterType.Bool) }
                                           })
        };

        public AccountDetails CreateAccountDetails(IDictionary<string, string> properties, ITaskLog taskLog)
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
                Password = password.ToSensitiveString(),
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
            public const string CreateAccountName = "create-azureaccount";

            public const string NameAttribute = "name";
            public const string UpdateIfExistingAttribute = "updateIfExisting";
            public const string SubscriptionAttribute = "azureSubscriptionId";
            public static class ServicePrincipal
            {
                public const string ApplicationAttribute = "azureApplicationId";
                public const string TenantAttribute = "azureTenantId";
                public const string PasswordAttribute = "azurePassword";
                public const string EnvironmentAttribute = "azureEnvironment";
                public const string BaseUriAttribute = "azureBaseUri";
                public const string ResourceManagementBaseUriAttribute = "azureResourceManagementBaseUri";
            }
        }
    }
}