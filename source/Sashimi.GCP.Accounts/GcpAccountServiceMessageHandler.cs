using System.Collections.Generic;
using System.Linq;
using Octopus.Data.Model;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.GCP.Accounts
{
    class GcpAccountServiceMessageHandler : ICreateAccountDetailsServiceMessageHandler
    {
        public string AuditEntryDescription => "GCP Account";
        public string ServiceMessageName => CreateGcpAccountMessagePropertyNames.Name;
        public IEnumerable<ScriptFunctionRegistration> ScriptFunctionRegistrations { get; } = Enumerable.Empty<ScriptFunctionRegistration>();

        public AccountDetails CreateAccountDetails(IDictionary<string, string> properties)
        {
            properties.TryGetValue(CreateGcpAccountMessagePropertyNames.ServiceAccountEmail, out var serviceAccountEmail);
            properties.TryGetValue(CreateGcpAccountMessagePropertyNames.Json, out var json);

            return new GcpAccountDetails
            {
                ServiceAccountEmail = serviceAccountEmail,
                Json = json?.ToSensitiveString()
            };
        }

        internal static class CreateGcpAccountMessagePropertyNames
        {
            public const string Name = "create-gcpaccount";
            public const string ServiceAccountEmail = "serviceaccountemail";
            public const string Json = "json";
        }
    }
}