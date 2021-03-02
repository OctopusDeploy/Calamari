using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.Azure;
using Calamari.AzureAppService.Behaviors;
using Calamari.AzureAppService.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Shared;
using Calamari.Tests.Shared.Helpers;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class AppServiceSettingsBehaviorFixture
    {
        private string clientId;
        private string clientSecret;
        private string tenantId;
        private string subscriptionId;
        private string webappName;
        private string resourceGroupName;
        private string slotName;
        private StringDictionary existingSettings;
        private ResourceGroupsOperations resourceGroupClient;
        private string authToken;
        private WebSiteManagementClient webMgmtClient;
        private Site site;

        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ??
                DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ??
                DefaultVariables.ActiveDirectoryEndpoint;

            resourceGroupName = Guid.NewGuid().ToString();
            
            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            var resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "eastus";

            authToken = await Auth.GetAuthTokenAsync(activeDirectoryEndpointBaseUri,
                resourceManagementEndpointBaseUri, tenantId, clientId, clientSecret);

            var resourcesClient = new ResourcesManagementClient(subscriptionId,
                new ClientSecretCredential(tenantId, clientId, clientSecret));

            resourceGroupClient = resourcesClient.ResourceGroups;

            var resourceGroup = new ResourceGroup(resourceGroupLocation);
            resourceGroup = await resourceGroupClient.CreateOrUpdateAsync(resourceGroupName, resourceGroup);

            webMgmtClient = new WebSiteManagementClient(new TokenCredentials(authToken))
            {
                SubscriptionId = subscriptionId,
                HttpClient = {BaseAddress = new Uri(DefaultVariables.ResourceManagementEndpoint)},
            };

            var svcPlan = await webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(resourceGroup.Name, resourceGroup.Name,
                new AppServicePlan(resourceGroup.Location));

            site = await webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroup.Name, resourceGroup.Name,
                new Site(resourceGroup.Location) {ServerFarmId = svcPlan.Id});
            
            existingSettings = new StringDictionary
            {
                Properties = new Dictionary<string, string> { { "ExistingSetting", "Foo" },{"ReplaceSetting","Foo"} }
            };

            webappName = site.Name;
        }

        [OneTimeTearDown]
        public async Task CleanupCode()
        {
            await resourceGroupClient.StartDeleteAsync(resourceGroupName);
        }
        
        [Test]
        public async Task TestSiteSettings()
        {
            await webMgmtClient.WebApps.UpdateApplicationSettingsWithHttpMessagesAsync(resourceGroupName, site.Name,
                existingSettings);

            var iVars = new CalamariVariables();
            AddVariables(iVars);
            var runningContext = new RunningDeployment("", iVars);
            iVars.Add("Greeting", "Calamari");
            
            var appSettings = BuildAppSettingsJson(new[]
            {
                ("MyFirstAppSetting", "Foo", true),
                ("MySecondAppSetting", "bar", false),
                ("ReplaceSetting", "Bar", false)
            });
            
            iVars.Add(SpecialVariables.Action.Azure.AppSettings, appSettings.json);
            
            await new AzureAppServiceSettingsBehaviour(new InMemoryLog()).Execute(runningContext);

            await AssertAppSettings(appSettings.setting);
        }

        [Test]
        public async Task TestSlotSettings()
        {
            var slotName = "stage";
            this.slotName = slotName;

            await webMgmtClient.WebApps.BeginCreateOrUpdateSlotAsync(resourceGroupName, resourceGroupName, site,
                slotName);
            
            var existingSettingsTask = webMgmtClient.WebApps.UpdateApplicationSettingsSlotWithHttpMessagesAsync(resourceGroupName,
                site.Name, existingSettings, slotName);

            var iVars = new CalamariVariables();
            AddVariables(iVars);
            var runningContext = new RunningDeployment("", iVars);
            iVars.Add("Greeting", slotName);
            iVars.Add("Octopus.Action.Azure.DeploymentSlot", slotName);

            var settings = BuildAppSettingsJson(new[]
            {
                ("FirstSetting", "Foo", true),
                ("MySecondAppSetting", "Baz", false),
                ("MyDeploySlotSetting", slotName, false),
                ("ReplaceSetting", "Foo", false)
            });

            iVars.Add(SpecialVariables.Action.Azure.AppSettings, settings.json);

            await existingSettingsTask;

            await new AzureAppServiceSettingsBehaviour(new InMemoryLog()).Execute(runningContext);
            await AssertAppSettings(settings.setting);
        }

        private void AddVariables(CalamariVariables vars)
        {
            vars.Add(AccountVariables.ClientId, clientId);
            vars.Add(AccountVariables.Password, clientSecret);
            vars.Add(AccountVariables.TenantId, tenantId);
            vars.Add(AccountVariables.SubscriptionId, subscriptionId);
            vars.Add("Octopus.Action.Azure.ResourceGroupName", resourceGroupName);
            vars.Add("Octopus.Action.Azure.WebAppName", webappName);
        }

        private (string json, IEnumerable<AppSetting> setting) BuildAppSettingsJson(IEnumerable<(string name, string value, bool isSlotSetting)> settings)
        {
            var appSettings = settings.Select(setting => new AppSetting
                {Name = setting.name, Value = setting.value, SlotSetting = setting.isSlotSetting});
            
            return (JsonConvert.SerializeObject(appSettings), appSettings);
        }

        async Task AssertAppSettings(IEnumerable<AppSetting> expectedSettings)
        {
            // Update existing settings with new replacement values
            var expectedSettingsArray = expectedSettings as AppSetting[] ?? expectedSettings.ToArray();
            foreach (var (name, value, _) in expectedSettingsArray.Where(x =>
                existingSettings.Properties.ContainsKey(x.Name)))
            {
                existingSettings.Properties[name] = value;
            }

            // for each existing setting that isn't defined in the expected settings object, add it
            var expectedList = expectedSettingsArray.ToList();
            foreach (var kvp in existingSettings.Properties.Where(x =>
                expectedSettingsArray.All(y => y.Name != x.Key)))
            {
                expectedList.Add(new AppSetting {Name = kvp.Key, Value = kvp.Value, SlotSetting = false});
            }
            
            // Get the settings from the webapp
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(webappName, slotName, resourceGroupName);
            
            var settings = await AppSettingsManagement.GetAppSettingsAsync(webMgmtClient, authToken, targetSite);

            CollectionAssert.AreEquivalent(expectedList, settings);
        }
    }
}