using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Octopus.Server.Extensibility.HostServices.Mapping;
using Octostache;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.AzureCloudService
{
    class AzureSubscriptionTypeProvider : IAccountTypeProvider
    {
        public AccountDetails CreateViaServiceMessage(IDictionary<string, string> properties)
        {
            throw new NotImplementedException();
        }

        public ServiceMessageValidationResult IsServiceMessageValid(IDictionary<string, string> messageProperties, VariableDictionary variables)
        {
            throw new NotImplementedException();
        }

        public string AuditEntryDescription => $"{AccountType.Value} account";
        public string ServiceMessageName => "not-implemented";

        public AccountType AccountType => AccountTypes.AzureSubscriptionAccountType;
        public Type ModelType => typeof(AzureSubscriptionDetails);
        public Type ApiType => typeof(AzureSubscriptionAccountResource);

        public IValidator Validator { get; } = new AzureSubscriptionValidator();
        public IVerifyAccount Verifier { get; } = new AzureSubscriptionAccountVerifier();

        public IEnumerable<(string key, object value)> GetMetric(IReadOnlyCollection<AccountDetails> details)
        {
            var total = details.Count(accountDetails => accountDetails.AccountType == AccountType);

            yield return ("azuresubscriptionaccount", total);
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureSubscriptionAccountResource, AzureSubscriptionDetails>();
        }
    }
}