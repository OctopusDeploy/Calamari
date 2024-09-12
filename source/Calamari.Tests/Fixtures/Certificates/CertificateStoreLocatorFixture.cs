using System.Security.Cryptography.X509Certificates;
using Calamari.Integration.Certificates;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Certificates
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    public class CertificateStoreLocatorFixture
    {
        [Test]
        public void GetStoreNames_ShouldContainMyStore()
        {
            var certificateStores = WindowsCertificateStoreLocator.GetStoreNames(StoreLocation.LocalMachine);
            certificateStores.Should().Contain(StoreName.My.ToString());
        }
    }
}