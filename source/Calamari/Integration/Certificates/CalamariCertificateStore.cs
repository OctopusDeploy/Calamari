using System;
using System.Security.Cryptography.X509Certificates;
using Calamari.Shared;
using Calamari.Shared.Certificates;
using Octopus.CoreUtilities;
using Octostache;

namespace Calamari.Integration.Certificates
{
    public class CalamariCertificateStore : ICertificateStore
    {
        public X509Certificate2 GetOrAdd(string thumbprint, byte[] bytes)
        {
            return GetOrAdd(thumbprint, bytes, null, null, new X509Store("Octopus", StoreLocation.CurrentUser), true);
        }

        public X509Certificate2 GetOrAdd(string thumbprint, byte[] bytes, StoreName storeName)
        {
            return GetOrAdd(thumbprint, bytes, null, null, new X509Store(storeName, StoreLocation.CurrentUser), true);
        }

        public X509Certificate2 GetOrAdd(VariableDictionary variables, string certificateVariable, string storeName, string storeLocation = "CurrentUser")
        {
            var location = (StoreLocation) Enum.Parse(typeof(StoreLocation), storeLocation);
            var name = (StoreName) Enum.Parse(typeof(StoreName), storeName);
            return GetOrAdd(variables, certificateVariable, name, location);
        }

        public X509Certificate2 GetOrAdd(VariableDictionary variables, string certificateVariable, StoreName storeName, StoreLocation storeLocation = StoreLocation.CurrentUser)
        {
            var pfxBytes = Convert.FromBase64String(variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Pfx}"));
            var thumbprint = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Thumbprint}");
            var password = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Password}");
            var subject = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Subject}");

            return GetOrAdd(thumbprint, pfxBytes, password, subject, new X509Store(storeName, storeLocation));
        }

        static X509Certificate2 GetOrAdd(string thumbprint, byte[] bytes, string password, string subject, X509Store store, bool privateKeyExportable = false)
        {
            var certificate = FindCertificateInStore(thumbprint, store);
            if (certificate.Some())
                return certificate.Value;

            AddCertificateToStore(bytes, password, subject, store, privateKeyExportable);

            return FindCertificateInStore(thumbprint, store).Value;
        }

        static Maybe<X509Certificate2> FindCertificateInStore(string thumbprint, X509Store store)
        {
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
            store.Open(OpenFlags.ReadOnly);

            try
            {
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (found.Count != 0 && found[0].HasPrivateKey)
                {
                    var certificate = found[0];
                    Log.Info($"Located certificate '{certificate.SubjectName.Name}' in Cert:\\{store.Location}\\{store.Name}");
                    return certificate.AsSome();
                }

                return Maybe<X509Certificate2>.None;
            }
            finally
            {
                store.Close();
            }
#else
            return Maybe<X509Certificate2>.None;
#endif
        }

        static void AddCertificateToStore(byte[] certificateBytes, string password, string subject, X509Store store, bool privateKeyExportable)
        {
#if WINDOWS_CERTIFICATE_STORE_SUPPORT
            Log.Info($"Adding certificate '{subject}' into Cert:\\{store.Location}\\{store.Name} {(privateKeyExportable ? " (marked exportable)" : " (not exportable)")}");
            try
            {
                WindowsX509CertificateStore.ImportCertificateToStore(certificateBytes, password, store.Location, store.Name, privateKeyExportable);
            }
            catch (Exception)
            {
                Log.Error("Exception while attempting to add certificate to store");
                throw;
            }
#endif
        }
    }
}