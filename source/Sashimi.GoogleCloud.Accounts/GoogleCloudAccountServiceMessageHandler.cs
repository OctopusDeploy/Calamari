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
        public string AuditEntryDescription => "GoogleCloud Account";
        public string ServiceMessageName => CreateGoogleCloudAccountMessagePropertyNames.Name;
        public IEnumerable<ScriptFunctionRegistration> ScriptFunctionRegistrations { get; } = Enumerable.Empty<ScriptFunctionRegistration>();

        public AccountDetails CreateAccountDetails(IDictionary<string, string> properties)
        {
            properties.TryGetValue(CreateGoogleCloudAccountMessagePropertyNames.ServiceAccountEmail, out var serviceAccountEmail);
            properties.TryGetValue(CreateGoogleCloudAccountMessagePropertyNames.Json, out var json);

            return new GoogleCloudAccountDetails
            {
                ServiceAccountEmail = serviceAccountEmail,
                Json = json?.ToSensitiveString()
            };
        }

        internal static class CreateGoogleCloudAccountMessagePropertyNames
        {
            public const string Name = "create-GoogleCloudaccount";
            public const string ServiceAccountEmail = "serviceaccountemail";
            public const string Json = "json";
        }
    }
}