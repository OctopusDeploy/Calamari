using System;
using System.Security.Cryptography.X509Certificates;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Certificates;

namespace Calamari.Deployment.Conventions
{
    public class EnsureCertificateInstalledInStoreConvention : IInstallConvention
    {
        private readonly ICertificateStore certificateStore;
        private readonly string certificateIdVariableName;
        private readonly string? storeLocationVariableName;
        private readonly string? storeNameVariableName;

        public EnsureCertificateInstalledInStoreConvention(
            ICertificateStore certificateStore,
            string certificateIdVariableName, string? storeLocationVariableName = null, string? storeNameVariableName = null)
        {
            this.certificateStore = certificateStore;
            this.certificateIdVariableName = certificateIdVariableName;
            this.storeLocationVariableName = storeLocationVariableName;
            this.storeNameVariableName = storeNameVariableName;
        }

        public void Install(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            var clientCertVariable = variables.Get(certificateIdVariableName);
            if (!string.IsNullOrEmpty(clientCertVariable))
            {
                EnsureCertificateInStore(variables, clientCertVariable);
            }
        }

        void EnsureCertificateInStore(IVariables variables, string certificateVariable)
        {
            var storeLocation = StoreLocation.LocalMachine;
            if (!string.IsNullOrWhiteSpace(storeLocationVariableName) && Enum.TryParse(variables.Get(storeLocationVariableName, StoreLocation.LocalMachine.ToString()), out StoreLocation storeLocationOverride))
                storeLocation = storeLocationOverride;
            var storeName = StoreName.My;
            if (!string.IsNullOrWhiteSpace(storeNameVariableName) && Enum.TryParse(variables.Get(storeNameVariableName, StoreName.My.ToString()), out StoreName storeNameOverride))
                storeName = storeNameOverride;

            certificateStore.GetOrAdd(variables, certificateVariable, storeName, storeLocation);
        }
    }
}