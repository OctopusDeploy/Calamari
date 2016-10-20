using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Calamari.Integration.Certificates
{
    public class PrivateKeyAccessRule
    {
        public PrivateKeyAccessRule(string identity, PrivateKeyAccess access)
        {
            Identity = identity;
            Access = access;
        } 

        public string Identity { get; }
        public PrivateKeyAccess Access { get; }

        public static ICollection<PrivateKeyAccessRule> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<PrivateKeyAccessRule>>(json, JsonSerializerSettings);
        }

        public static CryptoKeySecurity CreateCryptoKeySecurity(ICollection<PrivateKeyAccessRule> rules)
        {
            if (rules == null)
                return new CryptoKeySecurity();

           var security = new CryptoKeySecurity();

            foreach (var rule in rules)
            {
                switch (rule.Access)
                {
                    case PrivateKeyAccess.ReadOnly:
                        security.AddAccessRule(new CryptoKeyAccessRule(rule.Identity, CryptoKeyRights.GenericRead, AccessControlType.Allow));
                        break;
                    case PrivateKeyAccess.FullControl:
                        // We use 'GenericAll' here rather than 'FullControl' as 'FullControl' doesn't correctly set the access for CNG keys
                        security.AddAccessRule(new CryptoKeyAccessRule(rule.Identity, CryptoKeyRights.GenericAll, AccessControlType.Allow));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return security;
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