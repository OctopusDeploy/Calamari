using System;
using System.Threading.Tasks;
using Azure.ResourceManager.AppService;
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
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName, resourceGroupName);
            
            var principalAccount = ServicePrincipalAccount.CreateFromKnownVariables(variables);
            var armClient = principalAccount.CreateArmClient();

            Log.Info("Performing soft restart of web app");
            switch (targetSite.HasSlot)
            {
                case true:
                    await armClient.GetWebSiteSlotResource(WebSiteSlotResource.CreateResourceIdentifier(principalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site, targetSite.Slot))
                                   .RestartSlotAsync();
                    break;
                case false:
                    await armClient.GetWebSiteResource(WebSiteResource.CreateResourceIdentifier(principalAccount.SubscriptionNumber, targetSite.ResourceGroupName, targetSite.Site))
                                   .RestartAsync();
                    break;
            }
        }
    }
}