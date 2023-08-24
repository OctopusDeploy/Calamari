using System;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureAppService.Behaviors
{
    public class LegacyAppDeployBehaviour : IDeployBehaviour
    {
        readonly LegacyAzureAppServiceDeployContainerBehavior containerBehaviour;
        readonly LegacyAzureAppServiceBehaviour appServiceBehaviour;

        ILog Log { get; }

        public LegacyAppDeployBehaviour(ILog log)
        {
            Log = log;
            containerBehaviour = new LegacyAzureAppServiceDeployContainerBehavior(log);
            appServiceBehaviour = new LegacyAzureAppServiceBehaviour(log);
        }

        public bool IsEnabled(RunningDeployment context) => !FeatureToggle.ModernAzureAppServiceSdkFeatureToggle.IsEnabled(context.Variables);

        public Task Execute(RunningDeployment context)
        {
            var deploymentType = context.Variables.Get(SpecialVariables.Action.Azure.DeploymentType);
            Log.Verbose($"Deployment type: {deploymentType}");

            return deploymentType switch
                   {
                       "Container" => containerBehaviour.Execute(context),
                       _ => appServiceBehaviour.Execute(context)
                   };
        }
    }
}
