using System;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Octopus.CoreUtilities.Extensions;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;

namespace Calamari.AzureAppService.Behaviors
{
    public class RestartAzureWebAppBehaviour : IDeployBehaviour
    {
        ILog Log { get; }

        public RestartAzureWebAppBehaviour(ILog log)
        {
            Log = log;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;
            var hasJwt = !variables.Get(AccountVariables.Jwt).IsNullOrEmpty();
            var account = hasJwt ? (IAzureAccount)new AzureOidcAccount(variables) : new AzureServicePrincipalAccount(variables);

            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);

            var targetSite = new AzureTargetSite(account.SubscriptionNumber, resourceGroupName, webAppName, slotName);

            var armClient = account.CreateArmClient();

            Log.Info("Performing soft restart of web app");
            await armClient.RestartWebSiteAsync(targetSite);
        }
    }
}