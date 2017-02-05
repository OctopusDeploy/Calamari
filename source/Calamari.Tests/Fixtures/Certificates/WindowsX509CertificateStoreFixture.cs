#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
using System;
using System.Security.Cryptography.X509Certificates;
using Calamari.Integration.Certificates;
using Calamari.Tests.Helpers.Certificates;
using NUnit.Framework;
using Calamari.Tests.Helpers;

namespace Calamari.Tests.Fixtures.Certificates
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Windows)]
    public class WindowsX509CertificateStoreFixture
    {
        [Test]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.LocalMachine, "My")]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.CurrentUser, "My")]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.LocalMachine, "Foo")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.LocalMachine, "My")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.CurrentUser, "My")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.CurrentUser, "Foo")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyNoPasswordId, StoreLocation.LocalMachine, "My")]
        [TestCase(SampleCertificate.FabrikamNoPrivateKeyId, StoreLocation.LocalMachine, "My")]
        public void CanImportCertificate(string sampleCertificateId, StoreLocation storeLocation, string storeName)
        {
            var sampleCertificate = SampleCertificate.SampleCertificates[sampleCertificateId];

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);

            WindowsX509CertificateStore.ImportCertificateToStore(Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password, 
                storeLocation, storeName, sampleCertificate.HasPrivateKey);

            sampleCertificate.AssertCertificateIsInStore(storeName, storeLocation);

            if (sampleCertificate.HasPrivateKey)
            {
                var certificate = sampleCertificate.GetCertificateFromStore(storeName, storeLocation);
                Assert.True(certificate.HasPrivateKey);
            }

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);
        }

        [Test]
        public void CanImportCertificateForUser()
        {
            // This test cheats a little bit, using the current user 
            var user = System.Security.Principal.WindowsIdentity.GetCurrent().Name; 
            var storeName = "My";
            var sampleCertificate = SampleCertificate.CapiWithPrivateKey;

            sampleCertificate.EnsureCertificateNotInStore(storeName, StoreLocation.CurrentUser);

            WindowsX509CertificateStore.ImportCertificateToStore(Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password, 
                user, storeName, sampleCertificate.HasPrivateKey);

            sampleCertificate.AssertCertificateIsInStore(storeName, StoreLocation.CurrentUser);

            sampleCertificate.EnsureCertificateNotInStore(storeName, StoreLocation.CurrentUser);
        }
    }
}
#endif