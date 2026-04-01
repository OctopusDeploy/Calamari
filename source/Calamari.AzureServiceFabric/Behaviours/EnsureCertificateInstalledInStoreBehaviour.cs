using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Integration.Certificates;

namespace Calamari.AzureServiceFabric.Behaviours
{
    class EnsureCertificateInstalledInStoreBehaviour : IDeployBehaviour
    {
        readonly ICertificateStore certificateStore;
        readonly string certificateIdVariableName = SpecialVariables.Action.ServiceFabric.ClientCertVariable;

        public EnsureCertificateInstalledInStoreBehaviour(ICertificateStore certificateStore)
        {
            this.certificateStore = certificateStore;
        }
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
                var storeName = variables.GetServiceFabricCertificateStoreName();
                var storeLocation = variables.GetServiceFabricCertificateStoreLocation();
                certificateStore.GetOrAdd(variables, clientCertVariable, storeName, storeLocation);
            }

            return Task.CompletedTask;
        }
    }
}