using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.HostServices.Mapping;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Azure.Accounts
{
    class AzureServicePrincipalAccountTypeProvider : IAccountTypeProvider
    {
        public AzureServicePrincipalAccountTypeProvider(Lazy<IOctopusHttpClientFactory> httpClientFactoryLazy)
        {
            Verifier = new AzureServicePrincipalAccountVerifier(httpClientFactoryLazy);
        }

        public AccountDetails CreateViaServiceMessage(IDictionary<string, string> properties)
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

        public AccountType AccountType { get; } = AccountTypes.AzureServicePrincipalAccountType;
        public Type ModelType { get; } = typeof(AzureServicePrincipalAccountDetails);
        public Type ApiType { get; } = typeof(AzureServicePrincipalAccountResource);
        public IValidator Validator { get; } = new AzureServicePrincipalAccountValidator();
        public IVerifyAccount Verifier { get; }

        public IEnumerable<(string key, object value)> GetFeatureUsage(IAccountMetricContext context)
        {
            var total = context.GetAccountDetails<AzureServicePrincipalAccountDetails>().Count();

            yield return ("azureserviceprincipalaccount", total);
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureServicePrincipalAccountResource, AzureServicePrincipalAccountDetails>();
        }

        public string AuditEntryDescription => "Azure Service Principal account";
        public string ServiceMessageName => CreateAzureAccountServiceMessagePropertyNames.Name;
    }
}