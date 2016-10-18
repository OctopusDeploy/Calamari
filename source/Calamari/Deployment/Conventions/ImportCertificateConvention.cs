using System;
using System.Security.Cryptography.X509Certificates;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Deployment.Conventions
{
    public class ImportCertificateConvention : IInstallConvention 
    {
        private readonly ICalamariFileSystem fileSystem;

        public ImportCertificateConvention(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            var storageFlags = CreateStorageFlags(variables);
            var certificate = CreateCertificate(variables, storageFlags, fileSystem);
            var store = CreateStore(variables);

            store.Open(OpenFlags.ReadWrite);

            if (store.Certificates.Contains(certificate))
            {
                store.Remove(certificate);
            }

            store.Add(certificate);

            store.Close();
        }

        private static X509Store CreateStore(CalamariVariableDictionary variables)
        {
            var storeLocation = (StoreLocation)Enum.Parse(typeof(StoreLocation), variables.Get(SpecialVariables.Action.Certificate.StoreLocation));
            var storeName = variables.Get(SpecialVariables.Action.Certificate.StoreName);
            var store = new X509Store(storeName, storeLocation);
            return store;
        }

        private static X509Certificate2 CreateCertificate(CalamariVariableDictionary variables, X509KeyStorageFlags storageFlags, ICalamariFileSystem fileSystem)
        {
            var certificateVariable = variables[SpecialVariables.Action.Certificate.CertificateVariable];
            var pfxBytes =
                Convert.FromBase64String(
                    variables[
                        SpecialVariables.Action.Certificate.GetCertificateVariablePropertyName(certificateVariable,
                            SpecialVariables.Action.Certificate.PfxProperty)]);

            X509Certificate2 certificate;

            string tempPfxPath;
            using (var tempPfxStream = fileSystem.CreateTemporaryFile(".pfx", out tempPfxPath))
            using (new TemporaryFile(tempPfxPath))
            {
                tempPfxStream.Write(pfxBytes, 0, pfxBytes.Length);
                tempPfxStream.Flush();

                certificate = new X509Certificate2(tempPfxPath, (string) null, storageFlags);
            }
            return certificate;
        }

        private static X509KeyStorageFlags CreateStorageFlags(CalamariVariableDictionary variables)
        {
            var storageFlags = X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet;
            if (variables.GetFlag(SpecialVariables.Action.Certificate.PrivateKeyExportable, false))
            {
                storageFlags = storageFlags | X509KeyStorageFlags.Exportable;
            }
            return storageFlags;
        }
    }
}