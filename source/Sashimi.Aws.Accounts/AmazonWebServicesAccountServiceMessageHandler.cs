using System.Collections.Generic;
using Octopus.Data.Model;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Aws.Accounts
{
    class AmazonWebServicesAccountServiceMessageHandler : ICreateAccountDetailsServiceMessageHandler
    {
        public string AuditEntryDescription => "AWS Account";
        public string ServiceMessageName => CreateAwsAccountServiceMessagePropertyNames.Name;
        public AccountDetails CreateAccountDetails(IDictionary<string, string> properties)
        {
            properties.TryGetValue(CreateAwsAccountServiceMessagePropertyNames.AccessKey, out var accessKey);
            properties.TryGetValue(CreateAwsAccountServiceMessagePropertyNames.SecretKey, out var secretKey);

            return new AmazonWebServicesAccountDetails
            {
                AccessKey = accessKey,
                SecretKey = secretKey?.ToSensitiveString()
            };
        }

        internal static class CreateAwsAccountServiceMessagePropertyNames
        {
            public const string Name = "create-awsaccount";
            public const string SecretKey = "secretKey";
            public const string AccessKey = "accessKey";
        }
    }
}