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
        private string _activeDirectoryEndPoint = @"https://login.windows.net/";

        private string _clientId;
        private string _clientSecret;
        private string _tenantId;
        private string _subscriptionId;
        private string _webappName;
        private string _resourceGroupName;
        private string _slotName;
        private StringDictionary _existingSettings;
        private ResourceGroupsOperations _resourceGroupClient;
        private string _authToken;
        private AppSettingsRoot _testAppSettings;
        private WebSiteManagementClient _webMgmtClient;
        private Site _site;

        [OneTimeSetUp]
        public async Task Setup()
        {
            _resourceGroupName = Guid.NewGuid().ToString();
            
            _clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            _clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            _tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            _subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            var resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "eastus";

            _authToken = await Auth.GetAuthTokenAsync(_activeDirectoryEndPoint,
                DefaultVariables.ResourceManagementEndpoint, _tenantId, _clientId, _clientSecret);

            var resourcesClient = new ResourcesManagementClient(_subscriptionId,
                new ClientSecretCredential(_tenantId, _clientId, _clientSecret));

            _resourceGroupClient = resourcesClient.ResourceGroups;

            var resourceGroup = new ResourceGroup(resourceGroupLocation);
            resourceGroup = await _resourceGroupClient.CreateOrUpdateAsync(_resourceGroupName, resourceGroup);

            _webMgmtClient = new WebSiteManagementClient(new TokenCredentials(_authToken))
            {
                SubscriptionId = _subscriptionId,
                HttpClient = {BaseAddress = new Uri(DefaultVariables.ResourceManagementEndpoint)},
            };

            var svcPlan = await _webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(resourceGroup.Name, resourceGroup.Name,
                new AppServicePlan(resourceGroup.Location));

            _site = await _webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroup.Name, resourceGroup.Name,
                new Site(resourceGroup.Location) {ServerFarmId = svcPlan.Id});
            
            _existingSettings = new StringDictionary
            {
                Properties = new Dictionary<string, string> { { "ExistingSetting", "Foo" },{"ReplaceSetting","Foo"} }
            };

            _webappName = _site.Name;
        }

        [OneTimeTearDown]
        public async Task CleanupCode()
        {
            await _resourceGroupClient.StartDeleteAsync(_resourceGroupName);

            //foreach (var tempDir in _tempDirs)
            //{
            //    if(tempDir.Exists)
            //        tempDir.Delete(true);
            //}
        }


        [Test]
        public async Task TestSiteSettings()
        {
            await _webMgmtClient.WebApps.UpdateApplicationSettingsWithHttpMessagesAsync(_resourceGroupName, _site.Name,
                _existingSettings);

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
            
            iVars.Add(SpecialVariables.Action.Azure.AppSettings, appSettings);

            await new AzureAppServiceSettingsBehaviour(new InMemoryLog()).Execute(runningContext);

            await AssertAppSettings(JsonConvert.DeserializeObject<AppSettingsRoot>(appSettings));
        }

        [Test]
        public async Task TestSlotSettings()
        {
            var slotName = "stage";
            _slotName = slotName;

            await _webMgmtClient.WebApps.BeginCreateOrUpdateSlotAsync(_resourceGroupName, _resourceGroupName, _site,
                slotName);
            
            var existingSettingsTask = _webMgmtClient.WebApps.UpdateApplicationSettingsSlotWithHttpMessagesAsync(_resourceGroupName,
                _site.Name, _existingSettings, slotName);

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

            iVars.Add(SpecialVariables.Action.Azure.AppSettings, settings);

            await existingSettingsTask;

            await new AzureAppServiceSettingsBehaviour(new InMemoryLog()).Execute(runningContext);
            await AssertAppSettings(JsonConvert.DeserializeObject<AppSettingsRoot>(settings));
        }

        private void AddVariables(CalamariVariables vars)
        {
            vars.Add(AccountVariables.ClientId, _clientId);
            vars.Add(AccountVariables.Password, _clientSecret);
            vars.Add(AccountVariables.TenantId, _tenantId);
            vars.Add(AccountVariables.SubscriptionId, _subscriptionId);
            vars.Add("Octopus.Action.Azure.ResourceGroupName", _resourceGroupName);
            vars.Add("Octopus.Action.Azure.WebAppName", _webappName);
        }

        string BuildAppSettingsJson(IEnumerable<(string name, string value, bool isSlotSetting)> settings)
        {
            var appSettings = new AppSettingsRoot
            {
                AppSettings = settings.Select(setting => new AppSetting
                    {Name = setting.name, Value = setting.value, IsSlotSetting = setting.isSlotSetting}).ToList()
            };
            _testAppSettings = appSettings;

            return JsonConvert.SerializeObject(appSettings);
        }

        async Task AssertAppSettings(AppSettingsRoot expectedSettings)
        {
            // Update existing settings with new replacement values
            foreach (var (name, value, isslotsetting) in expectedSettings.AppSettings.Where(x =>
                _existingSettings.Properties.ContainsKey(x.Name)))
            {
                _existingSettings.Properties[name] = value;
            }

            // for each existing setting that isn't defined in the expected settings object, add it
            var expectedList = expectedSettings.AppSettings.ToList();
            foreach (var (name, value) in _existingSettings.Properties.Where(x =>
                expectedSettings.AppSettings.All(y => y.Name != x.Key)))
            {
                expectedList.Add(new AppSetting {Name = name, Value = value, IsSlotSetting = false});
            }

            expectedSettings.AppSettings = expectedList;

            // Get the settings from the webapp
            var targetSite = AzureWebAppHelper.GetAzureTargetSite(_webappName, _slotName);
            targetSite.ResourceGroupName = _resourceGroupName;

            var settings = await AppSettingsManagement.GetAppSettingsAsync(_webMgmtClient, _authToken, targetSite);

            CollectionAssert.AreEquivalent(expectedSettings.AppSettings, settings.AppSettings);
        }
    }
}