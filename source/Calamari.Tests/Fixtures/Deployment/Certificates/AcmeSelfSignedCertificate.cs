using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Calamari.Tests.Helpers;

namespace Calamari.Tests.Fixtures.Deployment.Certificates
{
    public static class AcmeSelfSignedCertificate
    {
        public const string Thumbprint = "E7D0BF9F1A62AED35BA22BED80F9795012A53636";

        public static void EnsureCertificateIsInStore()
        {
            var certificate = GetAcmeSelfSignedCertificate();
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(certificate);
            store.Close();
        }

        public static void EnsureCertificateNotInStore()
        {
            var certificate = GetAcmeSelfSignedCertificate();
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Remove(certificate);
            store.Close();
        }

        public static string Base64Bytes()
        {
            return Convert.ToBase64String(File.ReadAllBytes(GetAcmeCertificatePath()));

        }

        static X509Certificate2 GetAcmeSelfSignedCertificate()
        {
           return new X509Certificate2(GetAcmeCertificatePath(),
               (string)null, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable); 
        }


        static string GetAcmeCertificatePath()
        {
            return TestEnvironment.GetTestPath("Fixtures", "Deployment", "Certificates", "acme_self_signed.pfx");
        }
    }
}