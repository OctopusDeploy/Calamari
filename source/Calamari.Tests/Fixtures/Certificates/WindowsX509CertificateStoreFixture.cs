#if WINDOWS_CERTIFICATE_STORE_SUPPORT 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
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

        [Test]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.LocalMachine, "My")]
        [TestCase(SampleCertificate.CngPrivateKeyId, StoreLocation.LocalMachine, "Foo")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.LocalMachine, "My")]
        [TestCase(SampleCertificate.CapiWithPrivateKeyId, StoreLocation.LocalMachine, "Foo")]
        public void ImportExistingCertificateShouldNotOverwriteExistingPrivateKeyRights(string sampleCertificateId, StoreLocation storeLocation, string storeName)
        {
            var sampleCertificate = SampleCertificate.SampleCertificates[sampleCertificateId];

            sampleCertificate.EnsureCertificateNotInStore(storeName, storeLocation);

            WindowsX509CertificateStore.ImportCertificateToStore(Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password, 
                storeLocation, storeName, sampleCertificate.HasPrivateKey);

            WindowsX509CertificateStore.AddPrivateKeyAccessRules(sampleCertificate.Thumbprint, storeLocation, storeName, 
                new List<PrivateKeyAccessRule>
                {
                   new PrivateKeyAccessRule("BUILTIN\\Users", PrivateKeyAccess.FullControl) 
                });

            WindowsX509CertificateStore.ImportCertificateToStore(Convert.FromBase64String(sampleCertificate.Base64Bytes()), sampleCertificate.Password, 
                storeLocation, storeName, sampleCertificate.HasPrivateKey);

            var privateKeySecurity = WindowsX509CertificateStore.GetPrivateKeySecurity(sampleCertificate.Thumbprint, storeLocation, storeName);
            AssertHasPrivateKeyRights(privateKeySecurity, "BUILTIN\\Users", CryptoKeyRights.GenericAll);
        }

        void AssertHasPrivateKeyRights(CryptoKeySecurity privateKeySecurity, string identifier, CryptoKeyRights right)
        {
            var accessRules = privateKeySecurity.GetAccessRules(true, false, typeof(NTAccount));

            var found = accessRules.Cast<CryptoKeyAccessRule>()
                .Any(x => x.IdentityReference.Value == identifier && x.CryptoKeyRights.HasFlag(right));

            Assert.True(found, "Private-Key right was not set");
        }
    }
}
#endif