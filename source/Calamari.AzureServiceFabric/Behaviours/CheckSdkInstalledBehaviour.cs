using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureServiceFabric.Behaviours
{
    class CheckSdkInstalledBehaviour : IBeforePackageExtractionBehaviour
    {
        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            if (!ServiceFabricHelper.IsServiceFabricSdkInstalled())
                throw new CommandException("Could not find the Azure Service Fabric SDK on this server. This SDK is required before running Service Fabric commands.");

            return Task.CompletedTask;
        }
    }
}