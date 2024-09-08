#if WINDOWS_CERTIFICATE_STORE_SUPPORT
using System;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Calamari.Integration.Certificates
{
    public static class PrivateKeyAccessRuleExtensionMethods
    {
        public static IdentityReference GetIdentityReference(this PrivateKeyAccessRule privateKeyAccessRule)
        {
            var identity = privateKeyAccessRule.Identity;
            try
            {
                return new SecurityIdentifier(identity);
            }
            catch (ArgumentException)
            {
                return new NTAccount(identity);
            }
        }
        
        public static  CryptoKeyAccessRule ToCryptoKeyAccessRule(this PrivateKeyAccessRule privateKeyAccessRule)
        {
            var identity = privateKeyAccessRule.GetIdentityReference();
            switch (privateKeyAccessRule.Access)
            {
                case PrivateKeyAccess.ReadOnly:
                    return new CryptoKeyAccessRule(identity, CryptoKeyRights.GenericRead, AccessControlType.Allow);

                case PrivateKeyAccess.FullControl:
                    // We use 'GenericAll' here rather than 'FullControl' as 'FullControl' doesn't correctly set the access for CNG keys
                    return new CryptoKeyAccessRule(identity, CryptoKeyRights.GenericAll, AccessControlType.Allow);

                default:
                    throw new ArgumentOutOfRangeException(nameof(privateKeyAccessRule.Access));
            }
        }
    }
}
#endif