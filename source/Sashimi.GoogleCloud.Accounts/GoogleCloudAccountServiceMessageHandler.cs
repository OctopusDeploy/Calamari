using System.Collections.Generic;
using System.Linq;
using Octopus.Data.Model;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountServiceMessageHandler : ICreateAccountDetailsServiceMessageHandler
    {
        public string AuditEntryDescription => "Google Cloud Account";
        public string ServiceMessageName => CreateGoogleCloudAccountMessagePropertyNames.Name;
        public IEnumerable<ScriptFunctionRegistration> ScriptFunctionRegistrations { get; } = Enumerable.Empty<ScriptFunctionRegistration>();

        public AccountDetails CreateAccountDetails(IDictionary<string, string> properties)
        {
            properties.TryGetValue(CreateGoogleCloudAccountMessagePropertyNames.ServiceAccountEmail, out var serviceAccountEmail);
            properties.TryGetValue(CreateGoogleCloudAccountMessagePropertyNames.JsonKey, out var json);

            return new GoogleCloudAccountDetails
            {
                ServiceAccountEmail = serviceAccountEmail,
                JsonKey = json?.ToSensitiveString()
            };
        }

        internal static class CreateGoogleCloudAccountMessagePropertyNames
        {
            public const string Name = "create-googlecloudaccount";
            public const string ServiceAccountEmail = "serviceaccountemail";
            public const string JsonKey = "json";
        }
    }
}