using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.AzureAppService.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Rest;
using Newtonsoft.Json;

namespace Calamari.AzureAppService.Behaviors
{
    class AzureAppServiceDeployContainerBehavior : IDeployBehaviour
    {
        private ILog Log { get; }
        public AzureAppServiceDeployContainerBehavior(ILog log)
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

            var principalAccount = new ServicePrincipalAccount(variables);
            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            var rgName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName, rgName);

            var imageName = variables.Get(SpecialVariables.Action.Package.PackageId);
            var registryUrl = variables.Get(SpecialVariables.Action.Package.FeedId);
            var imageVersion = variables.Get(SpecialVariables.Action.Package.PackageVersion) ?? "latest";

            var token = await Auth.GetAuthTokenAsync(principalAccount);

            var webAppClient = new WebSiteManagementClient(new Uri(principalAccount.ResourceManagementEndpointBaseUri),
                    new TokenCredentials(token))
                {SubscriptionId = principalAccount.SubscriptionNumber};

            var startTime = DateTime.UtcNow;
            Log.Verbose(
                $"Logging current UTC time as {startTime}.  Will parse logs for init message occurring after this time.");

            Log.Info("Retrieving config to update image");
            var config = await webAppClient.WebApps.GetConfigurationAsync(targetSite);
            config.LinuxFxVersion = $@"DOCKER|{imageName}:{imageVersion}";

            Log.Info("Retrieving app settings to set registry url");
            var appSettings = await webAppClient.WebApps.ListApplicationSettingsAsync(targetSite);
            appSettings.Properties["DOCKER_REGISTRY_SERVER_URL"] = registryUrl;

            Log.Info("Updating application settings");
            await webAppClient.WebApps.UpdateApplicationSettingsAsync(targetSite, appSettings);

            Log.Info("Updating configuration");
            await webAppClient.WebApps.UpdateConfigurationAsync(targetSite, config);

            Log.Info("Restarting webapp (softly if possible)");
            await webAppClient.WebApps.RestartAsync(targetSite, true);
        }
    }
}
