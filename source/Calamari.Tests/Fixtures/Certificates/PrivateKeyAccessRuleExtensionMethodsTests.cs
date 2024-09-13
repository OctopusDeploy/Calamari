#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
using System.Security.Principal;
using Calamari.Integration.Certificates;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Certificates
{
    [TestFixture]
    public class PrivateKeyAccessRuleExtensionMethodsTests
    {
        [Test]
        public void GetIdentityReference_ShouldResolveNTAccount()
        {
            var originalIdentityReference = new NTAccount("Foo\\bar");
            var privateKeyAccessRule = new PrivateKeyAccessRule(originalIdentityReference.Value, PrivateKeyAccess.ReadOnly);

            var identityReference = privateKeyAccessRule.GetIdentityReference();

            identityReference.Should().BeOfType<NTAccount>();
            identityReference.Value.Should().Be(originalIdentityReference.Value);
        }

        [Test]
        public void GetIdentityReference_ShouldResolveSecurityIdentifier()
        {
            var originalIdentityReference = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
            var privateKeyAccessRule = new PrivateKeyAccessRule(originalIdentityReference.Value, PrivateKeyAccess.ReadOnly);

            var identityReference = privateKeyAccessRule.GetIdentityReference();

            identityReference.Should().BeOfType<SecurityIdentifier>();
            identityReference.Value.Should().Be(originalIdentityReference.Value);
                
        }
    }
}
#endif