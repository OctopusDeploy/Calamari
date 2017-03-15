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

        [Test]
        [TestCase(StoreLocation.LocalMachine, "My")]
        [TestCase(StoreLocation.CurrentUser, "My")]
        [TestCase(StoreLocation.LocalMachine, "Foo")]
        [TestCase(StoreLocation.CurrentUser, "Foo")]
        public void CanImportCertificateChain(StoreLocation storeLocation, string storeName)
        {
            var sampleCertificate = SampleCertificate.CertificateChain;
            const string intermediateAuthorityThumbprint = "2E5DEC036985A4028351FD8DF3532E49D7B34049";
            const string rootAuthorityThumbprint = "CC7ED077F0F292595A8166B01709E20C0884A5F8";
            // intermediate and root authority certificates are always imported to LocalMachine
            var intermediateAuthorityStore = new X509Store(StoreName.CertificateAuthority, StoreLocation.LocalMachine);
            var rootAuthorityStore = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            intermediateAuthorityStore.Open(OpenFlags.ReadWrite);
            rootAuthorityStore.Open(OpenFlags.ReadWrite);

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);
            WindowsX509CertificateStore.RemoveCertificateFromStore(intermediateAuthorityThumbprint, StoreLocation.LocalMachine, intermediateAuthorityStore.Name);
            WindowsX509CertificateStore.RemoveCertificateFromStore(rootAuthorityThumbprint, StoreLocation.LocalMachine, rootAuthorityStore.Name);

            WindowsX509CertificateStore.ImportCertificateToStore(Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password, 
                storeLocation, storeName, sampleCertificate.HasPrivateKey);

            sampleCertificate.AssertCertificateIsInStore(storeName, storeLocation);

            // Assert chain certificates were imported
            AssertCertificateInStore(intermediateAuthorityStore, intermediateAuthorityThumbprint);
            AssertCertificateInStore(rootAuthorityStore, rootAuthorityThumbprint);

            var certificate = sampleCertificate.GetCertificateFromStore(storeName, storeLocation);
            Assert.True(certificate.HasPrivateKey);

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);
            WindowsX509CertificateStore.RemoveCertificateFromStore(intermediateAuthorityThumbprint, StoreLocation.LocalMachine, intermediateAuthorityStore.Name);
            WindowsX509CertificateStore.RemoveCertificateFromStore(rootAuthorityThumbprint, StoreLocation.LocalMachine, rootAuthorityStore.Name);
        }

        private static void AssertCertificateInStore(X509Store store, string thumbprint)
        {
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            Assert.AreEqual(1, found.Count);
        }
    }
}
#endif