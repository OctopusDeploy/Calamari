using System;
using System.Security.Principal;
using Calamari.FullFrameworkTools.Contracts.WindowsCertStore;

namespace Calamari.FullFrameworkTools.WindowsCertStore
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