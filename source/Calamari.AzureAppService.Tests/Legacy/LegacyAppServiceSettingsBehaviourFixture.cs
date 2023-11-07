using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Behaviors;
using Calamari.AzureAppService.Behaviors.Legacy;
using Calamari.AzureAppService.Json;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using Newtonsoft.Json;
using NUnit.Framework;
using Polly.Retry;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;

namespace Calamari.AzureAppService.Tests.Legacy
{
    [TestFixture]
    public class LegacyAppServiceSettingsBehaviorFixture
    {
        private string clientId;
        private string clientSecret;
        private string tenantId;
        private string subscriptionId;
        private string webappName;
        private string resourceGroupName;
        private string slotName;
        private StringDictionary existingSettings;
        private ConnectionStringDictionary existingConnectionStrings;
        private ResourceGroupsOperations resourceGroupClient;
        private string authToken;
        private WebSiteManagementClient webMgmtClient;
        private Site site;
        private RetryPolicy retryPolicy;

        [OneTimeSetUp]
        public async Task Setup()
        {
            retryPolicy = RetryPolicyFactory.CreateForHttp429();
            
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            resourceGroupName = Guid.NewGuid().ToString();

            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            var resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "eastus";
            
            var account = new AzureServicePrincipalAccount(
                                                           subscriptionId,
                                                           clientId,
                                                           tenantId,
                                                           clientSecret,
                                                           "",
                                                           resourceManagementEndpointBaseUri,
                                                           activeDirectoryEndpointBaseUri);
            authToken = await new AzureAuthTokenService().GetAuthorizationToken(account, CancellationToken.None);

            var resourcesClient = new ResourcesManagementClient(subscriptionId,
                                                                new ClientSecretCredential(tenantId, clientId, clientSecret));

            resourceGroupClient = resourcesClient.ResourceGroups;

            var resourceGroup = new ResourceGroup(resourceGroupLocation);
            resourceGroup = await resourceGroupClient.CreateOrUpdateAsync(resourceGroupName, resourceGroup);


            webMgmtClient = new WebSiteManagementClient(new TokenCredentials(authToken))
            {
                SubscriptionId = subscriptionId,
                HttpClient = { BaseAddress = new Uri(DefaultVariables.ResourceManagementEndpoint) },
            };

            var svcPlan = await retryPolicy.ExecuteAsync(async () => await webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(resourceGroup.Name,
                                                                                                                                  resourceGroup.Name,
                                                                                                                                  new AppServicePlan(resourceGroup.Location)));

            site = await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroup.Name,
                                                                                                                   resourceGroup.Name,
                                                                                                                   new Site(resourceGroup.Location) { ServerFarmId = svcPlan.Id }));

            existingSettings = new StringDictionary
            {
                Properties = new Dictionary<string, string> { { "ExistingSetting", "Foo" }, { "ReplaceSetting", "Foo" } }
            };

            existingConnectionStrings = new ConnectionStringDictionary
            {
                Properties = new Dictionary<string, ConnStringValueTypePair>
                {
                    {
                        "ExistingConnectionString",
                        new ConnStringValueTypePair("ConnectionStringValue", ConnectionStringType.SQLAzure)
                    },
                    {
                        "ReplaceConnectionString",
                        new ConnStringValueTypePair("originalConnectionStringValue", ConnectionStringType.Custom)
                    }
                }
            };

            await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.UpdateConnectionStringsAsync(resourceGroupName, site.Name, existingConnectionStrings));

            webappName = site.Name;
        }

        [OneTimeTearDown]
        public async Task CleanupCode()
        {
            await resourceGroupClient.StartDeleteAsync(resourceGroupName);
        }

        [Test]
        public async Task TestSiteAppSettings()
        {
            await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.UpdateApplicationSettingsWithHttpMessagesAsync(resourceGroupName,
                                                                                                                                  site.Name,
                                                                                                                                  existingSettings));
            await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.UpdateConnectionStringsAsync(resourceGroupName,
                                                                                                                site.Name,
                                                                                                                existingConnectionStrings));

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

            await new LegacyAzureAppServiceSettingsBehaviour(new AzureAuthTokenService(), new InMemoryLog()).Execute(runningContext);

            await AssertAppSettings(appSettings.setting, new ConnectionStringDictionary());
        }

        [Test]
        public async Task TestSiteConnectionStrings()
        {
            await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.UpdateApplicationSettingsWithHttpMessagesAsync(resourceGroupName,
                                                                                                                                  site.Name,
                                                                                                                                  existingSettings));
            await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.UpdateConnectionStringsAsync(resourceGroupName,
                                                                                                                site.Name,
                                                                                                                existingConnectionStrings));

            var iVars = new CalamariVariables();
            AddVariables(iVars);
            var runningContext = new RunningDeployment("", iVars);
            iVars.Add("Greeting", "Calamari");

            var connectionStrings = BuildConnectionStringJson(new[]
            {
                ("ReplaceConnectionString", "replacedConnectionStringValue", ConnectionStringType.SQLServer, false),
                ("NewConnectionString", "newValue", ConnectionStringType.SQLAzure, false),
                ("ReplaceSlotConnectionString", "replacedSlotConnectionStringValue", ConnectionStringType.MySql, true)
            });

            iVars.Add(SpecialVariables.Action.Azure.ConnectionStrings, connectionStrings.json);

            await new LegacyAzureAppServiceSettingsBehaviour(new AzureAuthTokenService(), new InMemoryLog()).Execute(runningContext);

            await AssertAppSettings(new AppSetting[] { }, connectionStrings.connStrings);
        }

        [Test]
        public async Task TestSlotSettings()
        {
            var slotName = "stage";
            this.slotName = slotName;

            await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.BeginCreateOrUpdateSlotAsync(resourceGroupName,
                                                                                                                resourceGroupName,
                                                                                                                site,
                                                                                                                slotName));

            var existingSettingsTask = retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.UpdateApplicationSettingsSlotWithHttpMessagesAsync(resourceGroupName,
                                                                                                                                                           site.Name,
                                                                                                                                                           existingSettings,
                                                                                                                                                           slotName));

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

            var connectionStrings = BuildConnectionStringJson(new[]
            {
                ("NewKey", "newConnStringValue", ConnectionStringType.Custom, false),
                ("ReplaceConnectionString", "ChangedConnectionStringValue", ConnectionStringType.SQLServer, false),
                ("newSlotConnectionString", "ChangedConnectionStringValue", ConnectionStringType.SQLServer, true),
                ("ReplaceSlotConnectionString", "ChangedSlotConnectionStringValue", ConnectionStringType.Custom, true)
            });

            iVars.Add(SpecialVariables.Action.Azure.AppSettings, settings.json);
            iVars.Add(SpecialVariables.Action.Azure.ConnectionStrings, connectionStrings.json);

            await existingSettingsTask;

            await new LegacyAzureAppServiceSettingsBehaviour(new AzureAuthTokenService(), new InMemoryLog()).Execute(runningContext);
            await AssertAppSettings(settings.setting, connectionStrings.connStrings);
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
                                                  { Name = setting.name, Value = setting.value, SlotSetting = setting.isSlotSetting });

            return (JsonConvert.SerializeObject(appSettings), appSettings);
        }

        private (string json, ConnectionStringDictionary connStrings) BuildConnectionStringJson(
            IEnumerable<(string name, string value, ConnectionStringType type, bool isSlotSetting)> connStrings)
        {
            var connections = connStrings.Select(connstring => new LegacyConnectionStringSetting
            {
                Name = connstring.name, Value = connstring.value, Type = connstring.type,
                SlotSetting = connstring.isSlotSetting
            });
            var connectionsDict = new ConnectionStringDictionary
                { Properties = new Dictionary<string, ConnStringValueTypePair>() };

            foreach (var item in connStrings)
            {
                connectionsDict.Properties[item.name] = new ConnStringValueTypePair(item.value, item.type);
            }

            return (JsonConvert.SerializeObject(connections), connectionsDict);
        }

        async Task AssertAppSettings(IEnumerable<AppSetting> expectedAppSettings, ConnectionStringDictionary expectedConnStrings)
        {
            // Update existing settings with new replacement values
            var expectedSettingsArray = expectedAppSettings as AppSetting[] ?? expectedAppSettings.ToArray();
            foreach (var (name, value, _) in expectedSettingsArray.Where(x =>
                                                                             existingSettings.Properties.ContainsKey(x.Name)))
            {
                existingSettings.Properties[name] = value;
            }

            if (expectedConnStrings?.Properties != null && expectedConnStrings.Properties.Any())
            {
                foreach (var item in expectedConnStrings.Properties)
                {
                    existingConnectionStrings.Properties[item.Key] = item.Value;
                }
            }

            // for each existing setting that isn't defined in the expected settings object, add it
            var expectedSettingsList = expectedSettingsArray.ToList();

            expectedSettingsList.AddRange(existingSettings.Properties
                                                          .Where(x => expectedSettingsArray.All(y => y.Name != x.Key))
                                                          .Select(kvp =>
                                                                      new AppSetting { Name = kvp.Key, Value = kvp.Value, SlotSetting = false }));

            // Get the settings from the webapp
            var targetSite = new AzureTargetSite(subscriptionId, resourceGroupName, webappName, slotName);

            var settings = await AppSettingsManagement.GetAppSettingsAsync(webMgmtClient, authToken, targetSite);
            var connStrings = await AppSettingsManagement.GetConnectionStringsAsync(webMgmtClient, targetSite);

            CollectionAssert.AreEquivalent(expectedSettingsList, settings);

            foreach (var item in connStrings.Properties)
            {
                var existingItem = existingConnectionStrings.Properties[item.Key];

                Assert.AreEqual(existingItem.Value, item.Value.Value);
                Assert.AreEqual(existingItem.Type, item.Value.Type);
            }
            //CollectionAssert.AreEquivalent(existingConnectionStrings.Properties, connStrings.Properties);
        }
    }
}