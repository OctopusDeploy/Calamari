using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Octopus.Server.Extensibility.HostServices.Mapping;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Aws.Accounts
{
    class AmazonWebServicesAccountTypeProvider : IAccountTypeProvider
    {
        public AccountType AccountType { get; } = AccountTypes.AmazonWebServicesAccountType;
        public Type ModelType { get; } = typeof(AmazonWebServicesAccountDetails);
        public Type ApiType { get; } = typeof(AmazonWebServicesAccountResource);
        public IValidator Validator { get; } = new AmazonWebServicesAccountValidator();
        public IVerifyAccount Verifier { get; } = new AmazonWebServicesAccountVerifier();
        public ICreateAccountDetailsServiceMessageHandler? CreateAccountDetailsServiceMessageHandler { get; } = new AmazonWebServicesAccountServiceMessageHandler();

        public IEnumerable<(string key, object value)> GetFeatureUsage(IAccountMetricContext context)
        {
            var total = context.GetAccountDetails<AmazonWebServicesAccountDetails>().Count();

            yield return ("amazonwebservicesaccount", total);
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AmazonWebServicesAccountResource, AmazonWebServicesAccountDetails>();
        }
    }
}