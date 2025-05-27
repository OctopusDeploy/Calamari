using System;
using System.Threading.Tasks;
using Calamari.Azure.AppServices.Azure;
using Calamari.AzureAppService;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Octopus.CoreUtilities.Extensions;
using AccountVariables = Calamari.Azure.AppServices.Azure.AccountVariables;

namespace Calamari.Azure.AppServices.Behaviors
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