using System;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;

namespace Calamari.AzureAppService.Behaviors
{
    public class RestartAzureWebAppBehaviour : IDeployBehaviour
    {
        ILog Log { get; }
        
        public RestartAzureWebAppBehaviour(ILog log)
        {
            Log = log;
        }
        
        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            
            var principalAccount = ServicePrincipalAccount.CreateFromKnownVariables(variables);
            var token = await Auth.GetAuthTokenAsync(principalAccount);
            var webAppClient = new WebSiteManagementClient(new Uri(principalAccount.ResourceManagementEndpointBaseUri), new TokenCredentials(token))
                {SubscriptionId = principalAccount.SubscriptionNumber};
            
            var targetSite = new AzureTargetSite(principalAccount.SubscriptionNumber, resourceGroupName, webAppName, slotName);
            
            Log.Info("Performing soft restart of web app");
            await webAppClient.WebApps.RestartAsync(targetSite, true);
        }

    }
}