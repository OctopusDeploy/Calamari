using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using Newtonsoft.Json;

namespace Calamari.Integration.Certificates
{
    public class PrivateKeyAccessRule
    {
        public PrivateKeyAccessRule(string identity, PrivateKeyRights rights)
        {
            Identity = identity;
            Rights = rights;
        } 

        public string Identity { get; }
        public PrivateKeyRights Rights { get; }

        public static ICollection<PrivateKeyAccessRule> FromJson(string json)
        {
            return JsonConvert.DeserializeObject<List<PrivateKeyAccessRule>>(json);
        }

        public static CryptoKeySecurity CreateCryptoKeySecurity(ICollection<PrivateKeyAccessRule> rules)
        {
            if (rules == null)
                return new CryptoKeySecurity();

           var security = new CryptoKeySecurity();

            foreach (var rule in rules)
            {
                switch (rule.Rights)
                {
                    case PrivateKeyRights.ReadOnly:
                        security.AddAccessRule(new CryptoKeyAccessRule(rule.Identity, CryptoKeyRights.GenericRead, AccessControlType.Allow));
                        break;
                    case PrivateKeyRights.FullControl:
                        // We use 'GenericAll' here rather than 'FullControl' as 'FullControl' doesn't correctly set the access for CNG keys
                        security.AddAccessRule(new CryptoKeyAccessRule(rule.Identity, CryptoKeyRights.GenericAll, AccessControlType.Allow));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return security;
        }
    }
}