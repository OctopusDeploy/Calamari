using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Certificates;
using Octostache;
using System;
using System.Security.Cryptography.X509Certificates;

namespace Calamari.Azure.Deployment.Conventions
{
    public class EnsureServiceFabricCertificateExistsInStoreConvention : IInstallConvention
    {
        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            var clientCertVariable = variables.Get(SpecialVariables.Action.ServiceFabric.ClientCertVariable);
            if (!string.IsNullOrEmpty(clientCertVariable))
            {
//#if WINDOWS_CERTIFICATE_STORE_SUPPORT
                // Any certificate-variables used by IIS bindings must be placed in the
                // LocalMachine certificate store
                EnsureCertificateInStore(variables, SpecialVariables.Action.ServiceFabric.ClientCertVariable);
//#endif
            }
        }

//#if WINDOWS_CERTIFICATE_STORE_SUPPORT
        static void EnsureCertificateInStore(VariableDictionary variables, string certificateVariable)
        {
            var thumbprint = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Thumbprint}");
            var storeLocation = StoreLocation.LocalMachine;
            if (Enum.TryParse(variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreLocation), out StoreLocation storeLocationOverride))
                storeLocation = storeLocationOverride;
            var storeNameOverride = variables.Get(SpecialVariables.Action.ServiceFabric.CertificateStoreName);

            var storeName = FindCertificateInStore(thumbprint, storeLocation, storeNameOverride);
            if (storeName != null)
            {
                Log.Verbose($"Found existing certificate with thumbprint '{thumbprint}' in Cert:\\{storeLocation}\\{storeName}");
            }
            else
            {
                storeName = AddCertificateToStore(variables, certificateVariable, storeLocation, storeNameOverride ?? "My");
            }

            Log.SetOutputVariable(SpecialVariables.Action.IisWebSite.Output.CertificateStoreName, storeName, variables);
        }

        /// <summary>
        /// SF allows you to provide an override location for the storeLocation and storeName, so this method can optionally
        /// check for the certificate thumbprint in the given storeLocation/storeName.
        /// </summary>
        /// <param name="thumbprint"></param>
        /// <param name="storeLocation"></param>
        /// <param name="storeNameOverride"></param>
        /// <returns></returns>
        static string FindCertificateInStore(string thumbprint, StoreLocation storeLocation, string storeNameOverride)
        {
            foreach (var storeName in WindowsX509CertificateStore.GetStoreNames(StoreLocation.LocalMachine))
            {
                if (!string.IsNullOrEmpty(storeNameOverride) && storeName.Equals(storeNameOverride, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                var store = new X509Store(storeName, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);

                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
                if (found.Count != 0 && found[0].HasPrivateKey)
                    return storeName;

                store.Close();
            }

            return null;
        }

        static string AddCertificateToStore(VariableDictionary variables, string certificateVariable, StoreLocation storeLocation, string storeName)
        {
            var pfxBytes = Convert.FromBase64String(variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Pfx}"));
            var password = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Password}");
            var subject = variables.Get($"{certificateVariable}.{SpecialVariables.Certificate.Properties.Subject}");

            Log.Info($"Adding certificate '{subject}' into Cert:\\{storeLocation}\\{storeName}");

            try
            {
                WindowsX509CertificateStore.ImportCertificateToStore(pfxBytes, password, StoreLocation.LocalMachine, storeName, true);
                return storeName;
            }
            catch (Exception)
            {
                Log.Error("Exception while attempting to add certificate to store");
                throw;
            }
        }
//#endif
    }
}
