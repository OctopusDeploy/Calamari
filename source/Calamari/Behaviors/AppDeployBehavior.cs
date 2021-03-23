using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Behaviors
{
    public class AppDeployBehavior : IDeployBehaviour
    {
        private ILog Log { get; }

        public AppDeployBehavior(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            var deploymentType = context.Variables.Get(SpecialVariables.Action.Azure.DeploymentType);
            Log.Verbose($"Deployment type: {deploymentType}");
            
            switch (deploymentType)
            {
                case "Container":
                    return new AzureAppServiceDeployContainerBehavior(Log).Execute(context);

                // Default to zip deploy
                default:
                    return new AzureAppServiceBehaviour(Log).Execute(context);
            }
        }
    }
}
