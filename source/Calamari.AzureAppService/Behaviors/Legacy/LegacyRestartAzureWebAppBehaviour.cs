using System;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureAppService.Behaviors
{
    public class LegacyRestartAzureWebAppBehaviour : IDeployBehaviour
    {
        ILog Log { get; }

        public LegacyRestartAzureWebAppBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context) => !FeatureToggle.ModernAzureAppServiceSdkFeatureToggle.IsEnabled(context.Variables);

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);

            var hasAccessToken = !variables.Get(AccountVariables.AccessToken).IsNullOrEmpty();
            var account = hasAccessToken ? (IAzureAccount)new AzureOidcAccount(variables) : new ServicePrincipalAccount(variables);

            var token = await Auth.GetAuthTokenAsync(account);
            var webAppClient = new WebSiteManagementClient(new Uri(account.ResourceManagementEndpointBaseUri), new TokenCredentials(token))
                {SubscriptionId = account.SubscriptionNumber};

            var targetSite = new AzureTargetSite(account.SubscriptionNumber, resourceGroupName, webAppName, slotName);

            Log.Info("Performing soft restart of web app");
            await webAppClient.WebApps.RestartAsync(targetSite, true);
        }

    }
}