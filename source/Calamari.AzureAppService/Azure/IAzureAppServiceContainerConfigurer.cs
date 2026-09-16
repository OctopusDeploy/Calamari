using System.Threading.Tasks;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Calamari.Azure;
using Calamari.Azure.AppServices;
using Calamari.CloudAccounts;

namespace Calamari.AzureAppService.Azure
{
    // The single seam through which container deployment talks to Azure, so the deploy logic can be mocked in tests.
    public interface IAzureAppServiceContainerConfigurer
    {
        Task<bool> IsLinuxWebApp(IAzureAccount account, AzureTargetSite targetSite);
        Task<SiteConfigData> GetSiteConfig(IAzureAccount account, AzureTargetSite targetSite);
        Task<AppServiceConfigurationDictionary> GetAppSettings(IAzureAccount account, AzureTargetSite targetSite);
        Task UpdateAppSettings(IAzureAccount account, AzureTargetSite targetSite, AppServiceConfigurationDictionary appSettings);
        Task UpdateSiteConfig(IAzureAccount account, AzureTargetSite targetSite, SiteConfigData config);
    }

    public class AzureAppServiceContainerConfigurer : IAzureAppServiceContainerConfigurer
    {
        public async Task<bool> IsLinuxWebApp(IAzureAccount account, AzureTargetSite targetSite)
        {
            var armClient = account.CreateArmClient();
            var webSiteData = targetSite.HasSlot switch
                              {
                                  true => (await armClient.GetWebSiteSlotResource(WebSiteSlotResource.CreateResourceIdentifier(
                                                                                                                               targetSite.SubscriptionId,
                                                                                                                               targetSite.ResourceGroupName,
                                                                                                                               targetSite.Site,
                                                                                                                               targetSite.Slot))
                                                          .GetAsync()).Value.Data,
                                  false => (await armClient.GetWebSiteResource(WebSiteResource.CreateResourceIdentifier(
                                                                                                                        targetSite.SubscriptionId,
                                                                                                                        targetSite.ResourceGroupName,
                                                                                                                        targetSite.Site))
                                                           .GetAsync()).Value.Data
                              };

            //If the app service is a linux, it will contain linux in the kind string
            //possible values are found here: https://github.com/Azure/app-service-linux-docs/blob/master/Things_You_Should_Know/kind_property.md
            return webSiteData.Kind.ToLowerInvariant().Contains("linux");
        }

        public Task<SiteConfigData> GetSiteConfig(IAzureAccount account, AzureTargetSite targetSite)
            => account.CreateArmClient().GetSiteConfigDataAsync(targetSite);

        public Task<AppServiceConfigurationDictionary> GetAppSettings(IAzureAccount account, AzureTargetSite targetSite)
            => account.CreateArmClient().GetAppSettingsAsync(targetSite);

        public Task UpdateAppSettings(IAzureAccount account, AzureTargetSite targetSite, AppServiceConfigurationDictionary appSettings)
            => account.CreateArmClient().UpdateAppSettingsAsync(targetSite, appSettings);

        public Task UpdateSiteConfig(IAzureAccount account, AzureTargetSite targetSite, SiteConfigData config)
            => account.CreateArmClient().UpdateSiteConfigDataAsync(targetSite, config);
    }
}