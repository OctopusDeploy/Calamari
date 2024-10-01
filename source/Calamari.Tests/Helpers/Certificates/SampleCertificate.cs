
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Calamari.Integration.Certificates;
using NUnit.Framework;
using System.Linq;
using Calamari.Testing.Helpers;

namespace Calamari.Tests.Helpers.Certificates
{
    public class SampleCertificate
    {
        private readonly string fileName;

        public const string CngPrivateKeyId = "CngWithPrivateKey";
        public static readonly SampleCertificate CngWithPrivateKey = new SampleCertificate("cng_privatekey_password.pfx", "password", "CFED1F749A4D441F74F8D7345BA77C640E2449F0", true);

        public const string CapiWithPrivateKeyId = "CapiWithPrivateKey";
        public static readonly SampleCertificate CapiWithPrivateKey = new SampleCertificate("capi_self_signed_privatekey_password.pfx", "Password01!", "3FF420AB600A6C14E6EAF6AC25EF42EDCB96EE60", true);

        public const string CapiWithPrivateKeyNoPasswordId = "CapiWithPrivateKeyNoPassword";
        public static readonly SampleCertificate CapiWithPrivateKeyNoPassword = new SampleCertificate("capi_self_signed_privatekey_no_password.pfx", null, "E7D0BF9F1A62AED35BA22BED80F9795012A53636", true);

        public const string CertificateChainId = "CertificateChain";
        public static readonly SampleCertificate CertificateChain = new SampleCertificate("3-cert-chain.pfx", "hello world", "A11C309EFDF2864B1641F43A7A5B5019EB4CB816", true);

        public const string ChainSignedByLegacySha1RsaId = "ChainSignedByLegacySha1Rsa";
        public static readonly SampleCertificate ChainSignedByLegacySha1Rsa = new SampleCertificate("chain-signed-by-legacy-sha1-rsa.pfx", "Password01!", "47F51DED16E944AFC4B49B419D1C554974C895D2", true);

        public const string CertWithNoPrivateKeyId = "CertWithNoPrivateKey";
        public static readonly SampleCertificate CertWithNoPrivateKey = new SampleCertificate("cert-with-no-private-key.pfx", null, "FADD35F52F269FF0123D08396E2A6C1706496EE6", false);

        public static readonly IDictionary<string, SampleCertificate> SampleCertificates = new Dictionary<string, SampleCertificate>
        {
            {CngPrivateKeyId, CngWithPrivateKey},
            {CapiWithPrivateKeyId, CapiWithPrivateKey},
            {CapiWithPrivateKeyNoPasswordId, CapiWithPrivateKeyNoPassword},
            {CertificateChainId, CertificateChain},
            {ChainSignedByLegacySha1RsaId, ChainSignedByLegacySha1Rsa},
        };

        public SampleCertificate(string fileName, string password, string thumbprint, bool hasPrivateKey)
        {
            Password = password;
            Thumbprint = thumbprint;
            HasPrivateKey = hasPrivateKey;
            this.fileName = fileName;
        }

        public string Password { get; set; }
        public string Thumbprint { get; }
        public bool HasPrivateKey { get; }

        public string Base64Bytes()
        {
            return Convert.ToBase64String(File.ReadAllBytes(FilePath));
        }

        public void EnsureCertificateIsInStore(StoreName storeName, StoreLocation storeLocation)
        {
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);
            store.Add(LoadAsX509Certificate2());
            store.Close();
        }

        public void EnsureCertificateIsInStore(string storeName, StoreLocation storeLocation)
        {
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);
            store.Add(LoadAsX509Certificate2());
            store.Close();
        }

        public void EnsureCertificateNotInStore(StoreName storeName, StoreLocation storeLocation)
        {
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);

            EnsureCertificateNotInStore(store);
            store.Close();
        }

        public void EnsureCertificateNotInStore(string storeName, StoreLocation storeLocation)
        {
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);

            EnsureCertificateNotInStore(store);
            store.Close();
        }

        private void EnsureCertificateNotInStore(X509Store store)
        {
            var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, Thumbprint, false);

            if (certificates.Count == 0)
                return;

            new WindowsX509CertificateStore().RemoveCertificateFromStore(Thumbprint, store.Location, store.Name);
        }

        public void AssertCertificateIsInStore(string storeName, StoreLocation storeLocation)
        {
            Assert.NotNull(GetCertificateFromStore(storeName, storeLocation),
                $"Could not find certificate with thumbprint {Thumbprint} in store {storeLocation}\\{storeName}");
        }

        public X509Certificate2 GetCertificateFromStore(string storeName, StoreLocation storeLocation)
        {
            var store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadWrite);

            var foundCertificates = store.Certificates.Find(X509FindType.FindByThumbprint, Thumbprint, false);

            return foundCertificates.Count > 0
                ? foundCertificates[0]
                : null;
        }

#if WINDOWS_CERTIFICATE_STORE_SUPPORT
        public static void AssertIdentityHasPrivateKeyAccess(X509Certificate2 certificate, IdentityReference identity, CryptoKeyRights rights)
        {
            if (!certificate.HasPrivateKey)
                throw new Exception("Certificate does not have private key");

            var cspAlgorithm = certificate.PrivateKey as ICspAsymmetricAlgorithm;

            if (cspAlgorithm == null)
                throw new Exception("Private key is not a CSP key");

            var keySecurity = cspAlgorithm.CspKeyContainerInfo.CryptoKeySecurity;

            foreach(var accessRule in keySecurity.GetAccessRules(true, false, identity.GetType()).Cast<CryptoKeyAccessRule>())
            {
                if (accessRule.IdentityReference.Equals(identity) && accessRule.CryptoKeyRights.HasFlag(rights))
                    return;
            }

            throw new Exception($"Identity '{identity.ToString()}' does not have access right '{rights}' to private-key");
        }
#endif

        X509Certificate2 LoadAsX509Certificate2()
        {
            return new X509Certificate2(FilePath, Password,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }

        string FilePath => TestEnvironment.GetTestPath("Helpers", "Certificates", "SampleCertificateFiles", fileName);

    }
}