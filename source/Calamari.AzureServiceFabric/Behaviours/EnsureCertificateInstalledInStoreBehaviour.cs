using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureServiceFabric.Behaviours
{
    class EnsureCertificateInstalledInStoreBehaviour : IDeployBehaviour
    {
        readonly string certificateIdVariableName = SpecialVariables.Action.ServiceFabric.ClientCertVariable;
        readonly string storeLocationVariableName = SpecialVariables.Action.ServiceFabric.CertificateStoreLocation;
        readonly string storeNameVariableName = SpecialVariables.Action.ServiceFabric.CertificateStoreName;


        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;

            var clientCertVariable = variables.Get(certificateIdVariableName);
            if (!string.IsNullOrEmpty(clientCertVariable))
            {
                EnsureCertificateInStore(variables, clientCertVariable);
            }

            return Task.CompletedTask;
        }

        void EnsureCertificateInStore(IVariables variables, string certificateVariable)
        {
            var storeLocation = StoreLocation.LocalMachine;
            if (!string.IsNullOrWhiteSpace(storeLocationVariableName) && Enum.TryParse(variables.Get(storeLocationVariableName, StoreLocation.LocalMachine.ToString()), out StoreLocation storeLocationOverride))
                storeLocation = storeLocationOverride;
            var storeName = StoreName.My;
            if (!string.IsNullOrWhiteSpace(storeNameVariableName) && Enum.TryParse(variables.Get(storeNameVariableName, StoreName.My.ToString()), out StoreName storeNameOverride))
                storeName = storeNameOverride;

            CalamariCertificateStore.GetOrAdd(variables, certificateVariable, storeName, storeLocation);
        }
    }
}