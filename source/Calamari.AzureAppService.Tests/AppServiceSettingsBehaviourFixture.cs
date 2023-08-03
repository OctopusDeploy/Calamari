using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Behaviors;
using Calamari.AzureAppService.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class AppServiceSettingsBehaviorFixture : AppServiceIntegrationTest
    {
        string? slotName;
        AppServiceConfigurationDictionary existingSettings;
        ConnectionStringDictionary existingConnectionStrings;

        protected override async Task ConfigureTestResources(ResourceGroupResource resourceGroup)
        {
            var (_, webSiteResource) = await CreateAppServicePlanAndWebApp(resourceGroup);
            WebSiteResource = webSiteResource;

            existingSettings = new AppServiceConfigurationDictionary
            {
                Properties =
                {
                    ["ExistingSetting"] = "Foo",
                    ["ReplaceSetting"] = "Foo"
                }
            };

            existingConnectionStrings = new ConnectionStringDictionary
            {
                Properties =
                {
                    {
                        "ExistingConnectionString",
                        new ConnStringValueTypePair("ConnectionStringValue", ConnectionStringType.SqlAzure)
                    },
                    {
                        "ReplaceConnectionString",
                        new ConnStringValueTypePair("originalConnectionStringValue", ConnectionStringType.Custom)
                    }
                }
            };

            await WebSiteResource.UpdateConnectionStringsAsync(existingConnectionStrings);
        }

        [Test]
        public async Task TestSiteAppSettings()
        {
            await WebSiteResource.UpdateApplicationSettingsAsync(existingSettings);
            await WebSiteResource.UpdateConnectionStringsAsync(existingConnectionStrings);

            var iVars = new CalamariVariables();
            AddAzureVariables(iVars);
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

            await AssertAppSettings(appSettings.setting, new ConnectionStringDictionary());
        }

        [Test]
        public async Task TestSiteConnectionStrings()
        {
            await WebSiteResource.UpdateApplicationSettingsAsync(existingSettings);
            await WebSiteResource.UpdateConnectionStringsAsync(existingConnectionStrings);

            var iVars = new CalamariVariables();
            AddAzureVariables(iVars);
            var runningContext = new RunningDeployment("", iVars);
            iVars.Add("Greeting", "Calamari");

            var connectionStrings = BuildConnectionStringJson(new[]
            {
                ("ReplaceConnectionString", "replacedConnectionStringValue", ConnectionStringType.SqlServer, false),
                ("NewConnectionString", "newValue", ConnectionStringType.SqlAzure, false),
                ("ReplaceSlotConnectionString", "replacedSlotConnectionStringValue", ConnectionStringType.MySql, true)
            });

            iVars.Add(SpecialVariables.Action.Azure.ConnectionStrings, connectionStrings.json);

            await new AzureAppServiceSettingsBehaviour(new InMemoryLog()).Execute(runningContext);

            await AssertAppSettings(new AppSetting[] { }, connectionStrings.connStrings);
        }

        [Test]
        public async Task TestSlotSettings()
        {
            slotName =  "stage";

            var slotResponse = await WebSiteResource.GetWebSiteSlots()
                                              .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                   slotName,
                                                                   WebSiteResource.Data);

            var slotResource = slotResponse.Value;
            var existingSettingsTask = slotResource.UpdateApplicationSettingsSlotAsync(existingSettings);
                                                                                                                                                                   

            var iVars = new CalamariVariables();
            AddAzureVariables(iVars);
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
                ("ReplaceConnectionString", "ChangedConnectionStringValue", ConnectionStringType.SqlServer, false),
                ("newSlotConnectionString", "ChangedConnectionStringValue", ConnectionStringType.SqlServer, true),
                ("ReplaceSlotConnectionString", "ChangedSlotConnectionStringValue", ConnectionStringType.Custom, true)
            });

            iVars.Add(SpecialVariables.Action.Azure.AppSettings, settings.json);
            iVars.Add(SpecialVariables.Action.Azure.ConnectionStrings, connectionStrings.json);

            await existingSettingsTask;

            await new AzureAppServiceSettingsBehaviour(new InMemoryLog()).Execute(runningContext);
            await AssertAppSettings(settings.setting, connectionStrings.connStrings);
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
            var connections = connStrings.Select(connstring => new ConnectionStringSetting
            {
                Name = connstring.name, Value = connstring.value, Type = connstring.type,
                SlotSetting = connstring.isSlotSetting
            });
            var connectionsDict = new ConnectionStringDictionary();

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
            var targetSite = new AzureTargetSite(SubscriptionId, 
                                            ResourceGroupName, 
                                            WebSiteResource.Data.Name,
                                            slotName);


            var settings = await ArmClient.GetAppSettingsListAsync(targetSite);
            var connStrings = await ArmClient.GetConnectionStringsAsync(targetSite);

            CollectionAssert.AreEquivalent(expectedSettingsList, settings);

            foreach (var item in connStrings.Properties)
            {
                var existingItem = existingConnectionStrings.Properties[item.Key];

                Assert.AreEqual(existingItem.Value, item.Value.Value);
                Assert.AreEqual(existingItem.ConnectionStringType, item.Value.ConnectionStringType);
            }
            //CollectionAssert.AreEquivalent(existingConnectionStrings.Properties, connStrings.Properties);
        }
    }
}