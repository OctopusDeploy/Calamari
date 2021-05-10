using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Octopus.Server.Extensibility.HostServices.Mapping;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountTypeProvider : IAccountTypeProvider
    {
        public AccountType AccountType { get; } = AccountTypes.GoogleCloudAccountType;
        public Type ModelType { get; } = typeof(GoogleCloudAccountDetails);
        public Type ApiType { get; } = typeof(GoogleCloudAccountResource);
        public IValidator Validator { get; } = new GoogleCloudAccountValidator();
        public IVerifyAccount Verifier { get; } = new GoogleCloudAccountVerifier();
        public ICreateAccountDetailsServiceMessageHandler? CreateAccountDetailsServiceMessageHandler { get; } = new GoogleCloudAccountServiceMessageHandler();

        public IEnumerable<(string key, object value)> GetFeatureUsage(IAccountMetricContext context)
        {
            var total = context.GetAccountDetails<GoogleCloudAccountDetails>().Count();

            yield return ("GoogleCloudaccount", total);
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<GoogleCloudAccountResource, GoogleCloudAccountDetails>();
        }
    }
}