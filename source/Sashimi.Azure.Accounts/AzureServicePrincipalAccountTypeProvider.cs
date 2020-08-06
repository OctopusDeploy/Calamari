using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Octopus.Server.Extensibility.HostServices.Mapping;
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

        public AccountType AccountType { get; } = AccountTypes.AzureServicePrincipalAccountType;
        public Type ModelType { get; } = typeof(AzureServicePrincipalAccountDetails);
        public Type ApiType { get; } = typeof(AzureServicePrincipalAccountResource);
        public IValidator Validator { get; } = new AzureServicePrincipalAccountValidator();
        public IVerifyAccount Verifier { get; }
        public ICreateAccountDetailsServiceMessageHandler? CreateAccountDetailsServiceMessageHandler { get; } = new AzureServicePrincipalAccountServiceMessageHandler();

        public IEnumerable<(string key, object value)> GetFeatureUsage(IAccountMetricContext context)
        {
            var total = context.GetAccountDetails<AzureServicePrincipalAccountDetails>().Count();

            yield return ("azureserviceprincipalaccount", total);
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureServicePrincipalAccountResource, AzureServicePrincipalAccountDetails>();
        }
    }
}