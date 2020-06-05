using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.HostServices.Mapping;
using Octostache;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

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
            AzureServicePrincipalAccountDetails details = new AzureServicePrincipalAccountDetails
            {
                SubscriptionNumber = properties[CreateAzureAccountServiceMessagePropertyNames.SubscriptionAttribute],
                ClientId = properties[CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ApplicationAttribute],
                Password = properties[CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.PasswordAttribute].ToSensitiveString(),
                TenantId = properties[CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.TenantAttribute]
            };

            if (properties.ContainsPropertyWithValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute))
            {
                details.AzureEnvironment = properties[CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute];
                details.ActiveDirectoryEndpointBaseUri = properties[CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.BaseUriAttribute];
                details.ResourceManagementEndpointBaseUri = properties[CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ResourceManagementBaseUriAttribute];
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

        public IEnumerable<(string key, object value)> GetMetric(IReadOnlyCollection<AccountDetails> details)
        {
            var total = details.Count(accountDetails => accountDetails.AccountType == AccountType);

            yield return ("azureserviceprincipalaccount", total);
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureServicePrincipalAccountResource, AzureServicePrincipalAccountDetails>();
        }

        public ServiceMessageValidationResult IsServiceMessageValid(IDictionary<string, string> messageProperties, VariableDictionary variables)
        {
            var subscriptionAttributeValid = messageProperties.ContainsPropertyWithGuid(CreateAzureAccountServiceMessagePropertyNames.SubscriptionAttribute);
            var applicationAttributeValid = messageProperties.ContainsPropertyWithGuid(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ApplicationAttribute);
            var tenantAttributeValid = messageProperties.ContainsPropertyWithGuid(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.TenantAttribute);
            var passwordAttributeValid = messageProperties.ContainsPropertyWithValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.PasswordAttribute);
            bool isValid = subscriptionAttributeValid && applicationAttributeValid && tenantAttributeValid && passwordAttributeValid;

            var hasEnvironmentConfigured = messageProperties.ContainsPropertyWithValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.EnvironmentAttribute);
            var baseUriAttributeValid = messageProperties.ContainsPropertyWithValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.BaseUriAttribute);
            var resourceManagementUriAttributeValid = messageProperties.ContainsPropertyWithValue(CreateAzureAccountServiceMessagePropertyNames.ServicePrincipal.ResourceManagementBaseUriAttribute);
            if (hasEnvironmentConfigured)
            {
                isValid = isValid && baseUriAttributeValid && resourceManagementUriAttributeValid;
            }

            if (!isValid)
            {
                List<string> messages = new List<string>();
                if (!subscriptionAttributeValid) messages.Add("Subscription Id is missing or invalid");
                if (!applicationAttributeValid) messages.Add("Application Id is missing or invalid");
                if (!tenantAttributeValid) messages.Add("Tenant Id is missing or invalid");
                if (!passwordAttributeValid) messages.Add("Password is missing");

                if (hasEnvironmentConfigured)
                {
                    if (!baseUriAttributeValid) messages.Add("AD Endpoint Base Uri is missing");
                    if (!resourceManagementUriAttributeValid) messages.Add("Resource Management Base Uri is missing");
                }

                return ServiceMessageValidationResult.Invalid(messages);
            }

            return ServiceMessageValidationResult.Valid;
        }

        public string AuditEntryDescription => "Azure Service Principal account";
        public string ServiceMessageName => CreateAzureAccountServiceMessagePropertyNames.Name;
    }
}