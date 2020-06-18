using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.HostServices.Mapping;
using Octostache;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Aws.Accounts
{
    class AmazonWebServicesAccountTypeProvider : IAccountTypeProvider
    {
        public AccountDetails CreateViaServiceMessage(IDictionary<string, string> properties)
        {
            AmazonWebServicesAccountDetails accountDetails = new AmazonWebServicesAccountDetails();
            accountDetails.AccessKey = properties[CreateAwsAccountServiceMessagePropertyNames.AccessKey];
            accountDetails.SecretKey = properties[CreateAwsAccountServiceMessagePropertyNames.SecretKey].ToSensitiveString();
            return accountDetails;
        }

        public ServiceMessageValidationResult IsServiceMessageValid(IDictionary<string, string> properties, VariableDictionary variables)
        {
            var secretValid = properties.ContainsPropertyWithValue(CreateAwsAccountServiceMessagePropertyNames.SecretKey);
            var accessValid = properties.ContainsPropertyWithValue(CreateAwsAccountServiceMessagePropertyNames.AccessKey);

            if (!(secretValid && accessValid))
            {
                var messages = new List<string>();
                if (!secretValid) messages.Add("Secret Key is missing or invalid");
                if (!accessValid) messages.Add("Access Key is missing or invalid");

                return ServiceMessageValidationResult.Invalid(messages);
            }

            return ServiceMessageValidationResult.Valid;
        }

        public string AuditEntryDescription => "AWS Account";
        public string ServiceMessageName => CreateAwsAccountServiceMessagePropertyNames.Name;
        public AccountType AccountType { get; } = AccountTypes.AmazonWebServicesAccountType;
        public Type ModelType { get; } = typeof(AmazonWebServicesAccountDetails);
        public Type ApiType { get; } = typeof(AmazonWebServicesAccountResource);
        public IValidator Validator { get; } = new AmazonWebServicesAccountValidator();
        public IVerifyAccount Verifier { get; } = new AmazonWebServicesAccountVerifier();

        public IEnumerable<(string key, object value)> GetFeatureUsage(IAccountMetricContext context)
        {
            var total = context.GetAccountDetails<AmazonWebServicesAccountDetails>().Count();

            yield return ("amazonwebservicesaccount", total);
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AmazonWebServicesAccountResource, AmazonWebServicesAccountDetails>();
        }

        public static class CreateAwsAccountServiceMessagePropertyNames
        {
            public const string Name = "create-awsaccount";
            public const string SecretKey = "secretKey";
            public const string AccessKey = "accessKey";
        }
    }
}