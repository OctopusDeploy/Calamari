using System;
using System.Security.Principal;

namespace Calamari.Integration.Certificates
{
    public static class PrivateKeyAccessRuleExtensionMethods {
    
        public static IdentityReference GetIdentityReference(this PrivateKeyAccessRule privateKeyAccessRule)
        {
            try
            {
                return new SecurityIdentifier(privateKeyAccessRule.Identity);
            }
            catch (ArgumentException)
            {
                return new NTAccount(privateKeyAccessRule.Identity);
                
            }
        }
    }
}