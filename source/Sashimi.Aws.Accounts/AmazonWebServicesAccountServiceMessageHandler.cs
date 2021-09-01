using System;
using System.Collections.Generic;
using Octopus.Data.Model;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Aws.Accounts
{
    class AmazonWebServicesAccountServiceMessageHandler : ICreateAccountDetailsServiceMessageHandler
    {
        public string AuditEntryDescription => "AWS Account";
        public string ServiceMessageName => CreateAwsAccountServiceMessagePropertyNames.CreateAccountName;

        public IEnumerable<ScriptFunctionRegistration> ScriptFunctionRegistrations { get; } = new List<ScriptFunctionRegistration>
        {
            new("OctopusAwsAccount",
                "Creates a new Amazon Web Services Account.",
                CreateAwsAccountServiceMessagePropertyNames.CreateAccountName,
                new Dictionary<string, FunctionParameter>
                {
                    { CreateAwsAccountServiceMessagePropertyNames.NameAttribute, new FunctionParameter(ParameterType.String) },
                    { CreateAwsAccountServiceMessagePropertyNames.SecretKeyAttribute, new FunctionParameter(ParameterType.String) },
                    { CreateAwsAccountServiceMessagePropertyNames.AccessKeyAttribute, new FunctionParameter(ParameterType.String) },
                    { CreateAwsAccountServiceMessagePropertyNames.UpdateIfExistingAttribute, new FunctionParameter(ParameterType.Bool) }
                })
        };

        public AccountDetails CreateAccountDetails(IDictionary<string, string> properties, ITaskLog taskLog)
        {
            properties.TryGetValue(CreateAwsAccountServiceMessagePropertyNames.AccessKeyAttribute, out var accessKey);
            properties.TryGetValue(CreateAwsAccountServiceMessagePropertyNames.SecretKeyAttribute, out var secretKey);

            return new AmazonWebServicesAccountDetails
            {
                AccessKey = accessKey,
                SecretKey = secretKey.ToSensitiveString()
            };
        }

        internal static class CreateAwsAccountServiceMessagePropertyNames
        {
            public const string CreateAccountName = "create-awsaccount";

            public const string UpdateIfExistingAttribute = "updateIfExisting";
            public const string NameAttribute = "name";
            public const string SecretKeyAttribute = "secretKey";
            public const string AccessKeyAttribute = "accessKey";
        }
    }
}