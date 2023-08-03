using System;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureAppService.Behaviors
{
    public class AppDeployBehaviour : IDeployBehaviour
    {
        private ILog Log { get; }

        public AppDeployBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public Task Execute(RunningDeployment context)
        {
            var deploymentType = context.Variables.Get(SpecialVariables.Action.Azure.DeploymentType);
            Log.Verbose($"Deployment type: {deploymentType}");

            return deploymentType switch
                   {
                       "Container" => new AzureAppServiceDeployContainerBehaviourFactory(Log).Execute(context),
                       _ => new AzureAppServiceDeployBehaviourFactory(Log).Execute(context)
                   };
        }
    }
}
