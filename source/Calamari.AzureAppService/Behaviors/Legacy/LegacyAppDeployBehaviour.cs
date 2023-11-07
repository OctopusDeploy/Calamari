using System;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureAppService.Behaviors.Legacy
{
    public class LegacyAppDeployBehaviour : IDeployBehaviour
    {
        readonly LegacyAzureAppServiceDeployContainerBehavior containerBehaviour;
        readonly LegacyAzureAppServiceBehaviour appServiceBehaviour;

        ILog Log { get; }

        public LegacyAppDeployBehaviour(
            IAzureClientFactory azureClientFactory,
            IPublishingProfileService publishingProfileService,
            IBasicAuthService basicAuthService,
            IAzureAuthTokenService azureAuthTokenService,
            ILog log)
        {
            Log = log;
            containerBehaviour = new LegacyAzureAppServiceDeployContainerBehavior(azureAuthTokenService, log);
            appServiceBehaviour = new LegacyAzureAppServiceBehaviour(azureClientFactory, publishingProfileService, basicAuthService, azureAuthTokenService, log);
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
