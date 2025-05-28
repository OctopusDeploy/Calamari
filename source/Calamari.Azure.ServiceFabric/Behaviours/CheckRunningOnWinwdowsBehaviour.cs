using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Azure.ServiceFabric.Behaviours
{
    class CheckRunningOnWindowsBehaviour : IBeforePackageExtractionBehaviour
    {
        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            if (!CalamariEnvironment.IsRunningOnWindows)
            {
                throw new CommandException("Azure Service Fabric can only be executed on Windows.");
            }
            
            return Task.CompletedTask;
        }
    }
}