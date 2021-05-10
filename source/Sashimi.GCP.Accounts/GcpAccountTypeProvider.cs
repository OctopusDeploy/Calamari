using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Octopus.Server.Extensibility.HostServices.Mapping;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.GCP.Accounts
{
    class GcpAccountTypeProvider : IAccountTypeProvider
    {
        public AccountType AccountType { get; } = AccountTypes.GcpAccountType;
        public Type ModelType { get; } = typeof(GcpAccountDetails);
        public Type ApiType { get; } = typeof(GcpAccountResource);
        public IValidator Validator { get; } = new GcpAccountValidator();
        public IVerifyAccount Verifier { get; } = new GcpAccountVerifier();
        public ICreateAccountDetailsServiceMessageHandler? CreateAccountDetailsServiceMessageHandler { get; } = new GcpAccountServiceMessageHandler();

        public IEnumerable<(string key, object value)> GetFeatureUsage(IAccountMetricContext context)
        {
            var total = context.GetAccountDetails<GcpAccountDetails>().Count();

            yield return ("gcpaccount", total);
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<GcpAccountResource, GcpAccountDetails>();
        }
    }
}