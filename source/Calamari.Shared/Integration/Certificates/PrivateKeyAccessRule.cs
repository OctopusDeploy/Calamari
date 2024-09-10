using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Calamari.Integration.Certificates
{
    public class PrivateKeyAccessRule
    {
        [JsonConstructor]
        public PrivateKeyAccessRule(string identity, PrivateKeyAccess access)
            :this(new NTAccount(identity), access)
        {
        }

        public PrivateKeyAccessRule(IdentityReference identity, PrivateKeyAccess access)
        {
            Identity = identity;
            Access = access;
        }

        public IdentityReference Identity { get; }
        public PrivateKeyAccess Access { get; }

        public static ICollection<PrivateKeyAccessRule> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<PrivateKeyAccessRule>>(json, JsonSerializerSettings);
        }

        private static JsonSerializerSettings JsonSerializerSettings => new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>
            {
                new StringEnumConverter(),
            }
        };

    }
}
