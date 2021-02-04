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
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webAppName, slotName);
            targetSite.ResourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);

            var imageName = variables.Get(SpecialVariables.Action.Package.PackageId);
            var registryUrl = variables.Get(SpecialVariables.Action.Package.FeedId);

            var token = await Auth.GetAuthTokenAsync(principalAccount);

            var webAppClient = new WebSiteManagementClient(new Uri(principalAccount.ResourceManagementEndpointBaseUri),
                    new TokenCredentials(token))
                {SubscriptionId = principalAccount.SubscriptionNumber};
            
            var startTime = DateTime.UtcNow;
            Log.Verbose($"Logging current UTC time as {startTime}.  Will parse logs for init message occurring after this time.");

            Log.Info("Retrieving config to update image");
            var config = await webAppClient.WebApps.GetConfigurationAsync(targetSite);
            config.LinuxFxVersion = $@"DOCKER|{imageName}";

            Log.Info("Retrieving app settings to set registry url");
            var appSettings = await webAppClient.WebApps.ListApplicationSettingsAsync(targetSite);
            appSettings.Properties["DOCKER_REGISTRY_SERVER_URL"] = registryUrl;

            Log.Info("Updating application settings");
            await webAppClient.WebApps.UpdateApplicationSettingsAsync(targetSite, appSettings);

            Log.Info("Updating configuration");
            await webAppClient.WebApps.UpdateConfigurationAsync(targetSite, config);

            Log.Info("Restarting webapp (softly if possible)");
            await webAppClient.WebApps.RestartAsync(targetSite, true);
            
            var timeoutLength = int.Parse(variables.Get(SpecialVariables.Action.Azure.ContainerInitTimeout) ?? "600");

            var timeout = TimeSpan.FromSeconds(timeoutLength);
            var timeoutToken = new CancellationTokenSource(timeout);
            Log.Verbose($"Timeout set to {timeout.TotalSeconds:### 'seconds'} ({timeout.TotalMinutes:## 'minutes'})");
            Log.Verbose("retrieving logs to parse");

            //get the current logs so we can ignore it from the log stream and start parsing after we declare the new container to know when the new on is up and running
            await using var logStream =
                await webAppClient.WebApps.GetWebSiteContainerLogsAsync(targetSite.ResourceGroupName, targetSite.Site, timeoutToken.Token);
            
            try
            {
                Log.Verbose("Parsing logs waiting to initialize");
                await WaitToInitialize(new StreamReader(logStream), $"for site {targetSite.Site} initialized", startTime, timeoutToken.Token);
            }
            catch (OperationCanceledException ex)
            {
                var errorMsg =
                    $"failed to initialize container in the allotted time {timeout.TotalSeconds:### 'seconds'} ({timeout.TotalMinutes:## 'minutes'})";
                Log.Error(errorMsg);

                throw new TimeoutException(errorMsg, ex);
            }
        }

        private async Task WaitToInitialize(StreamReader reader, string initText, DateTime startTime, CancellationToken cancellationToken)
        {
            //var regexString = 
            var regex = new Regex(@"(^\d{4}(?:-\d{2}){2}T(?:\d{2}:*){3}.\d{3}Z).*for site.*initialized",
                RegexOptions.Compiled | RegexOptions.Multiline);
            
            do
            {
                var text = await reader.ReadLineAsync() ?? "";

                //if (text != null)

                var matches = regex.Matches(text);

                while (matches.Count == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    text = await reader.ReadToEndAsync();
                    matches = regex.Matches(text);
                }


                foreach (Match match in matches)
                {
                    var time = DateTime.Parse(match.Groups[1].Value).ToUniversalTime();
                    // if the start time is earlier than the time parsed from the logs https://docs.microsoft.com/en-us/dotnet/api/system.datetime.compare
                    if (DateTime.Compare(startTime, time) <= 0)
                    {
                        // we have initialized
                        return;
                    }
                }
            } while (!cancellationToken.IsCancellationRequested);
        }
    }
}
