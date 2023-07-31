using System;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

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
            var principalAccount = ServicePrincipalAccount.CreateFromKnownVariables(variables);
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            
            var targetSite = new AzureTargetSite(principalAccount.SubscriptionNumber, resourceGroupName, webAppName, slotName);
            
            var armClient = principalAccount.CreateArmClient();

            Log.Info("Performing soft restart of web app");
            await armClient.RestartWebSiteAsync(targetSite);
        }
    }
}