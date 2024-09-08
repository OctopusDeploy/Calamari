using System;
using System.Collections.Generic;
using System.Security.Principal;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Calamari.Integration.Certificates
{
    public class PrivateKeyAccessRule
    {
        [JsonConstructor]
        public PrivateKeyAccessRule(string identity, PrivateKeyAccess access)

        {
            Identity = identity;
            Access = access;
        }

        /*public PrivateKeyAccessRule(IdentityReference identity, PrivateKeyAccess access)
        {
            Identity = identity;
            Access = access;
        }*/

        /*public IdentityReference Identity { get; }*/
        public string Identity { get; }

        public IdentityReference GetIdentityReference()
        {
            return TryParse(Identity, out var temp) ? (IdentityReference)temp : new NTAccount(Identity);
        }
        
        
        public static bool TryParse(string value, out SecurityIdentifier result)
        {
            try
            {
                result = new SecurityIdentifier(value);
                return true;
            }
            catch (ArgumentException)
            {
                result = null;
                return false;
            }
        }
        
        
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
