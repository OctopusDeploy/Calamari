using System.Threading.Tasks;
using Calamari.Azure.AppServices;
using Calamari.AzureAppService.Azure;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Octopus.CoreUtilities.Extensions;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;

namespace Calamari.AzureAppService.Behaviors
{
    class AzureAppServiceContainerDeployBehaviour : IDeployBehaviour
    {
        private ILog Log { get; }
        private readonly IAzureAppServiceContainerConfigurer configurer;

        public AzureAppServiceContainerDeployBehaviour(ILog log, IAzureAppServiceContainerConfigurer configurer)
        {
            Log = log;
            this.configurer = configurer;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;

            var hasAccessToken = !variables.Get(AccountVariables.Jwt).IsNullOrEmpty();
            var account = hasAccessToken ? (IAzureAccount)new AzureOidcAccount(variables) : new AzureServicePrincipalAccount(variables);

            var webAppName = variables.Get(SpecialVariables.Action.Azure.WebAppName);
            var slotName = variables.Get(SpecialVariables.Action.Azure.WebAppSlot);
            var resourceGroupName = variables.Get(SpecialVariables.Action.Azure.ResourceGroupName);

            var targetSite = new AzureTargetSite(account.SubscriptionNumber, resourceGroupName, webAppName, slotName);

            var image = variables.Get(SpecialVariables.Action.Package.Image);
            var registryHost = variables.Get(SpecialVariables.Action.Package.Registry);
            var regUsername = variables.Get(SpecialVariables.Action.Package.Feed.Username);
            var regPwd = variables.Get(SpecialVariables.Action.Package.Feed.Password);

            Log.Info($"Updating web app to use image {image} from registry {registryHost}");

            Log.Verbose("Retrieving app service to determine operating system");
            var isLinuxWebApp = await configurer.IsLinuxWebApp(account, targetSite);

            Log.Verbose("Retrieving config (this is required to update image)");
            var config = await configurer.GetSiteConfig(account, targetSite);

            var newVersion = $"DOCKER|{image}";
            if (isLinuxWebApp)
            {
                Log.Verbose($"Updating LinuxFxVersion to {newVersion}");
                config.LinuxFxVersion = newVersion;
            }
            else
            {
                Log.Verbose($"Updating WindowsFxVersion to {newVersion}");
                config.WindowsFxVersion = newVersion;
            }

            Log.Verbose("Retrieving app settings");
            var appSettings = await configurer.GetAppSettings(account, targetSite);

            appSettings.Properties["DOCKER_REGISTRY_SERVER_URL"] = "https://" + registryHost;
            appSettings.Properties["DOCKER_REGISTRY_SERVER_USERNAME"] = regUsername;
            appSettings.Properties["DOCKER_REGISTRY_SERVER_PASSWORD"] = regPwd;

            Log.Info("Updating app settings with container registry");
            await configurer.UpdateAppSettings(account, targetSite, appSettings);

            Log.Info("Updating configuration with container image");
            await configurer.UpdateSiteConfig(account, targetSite, config);
        }
    }
}