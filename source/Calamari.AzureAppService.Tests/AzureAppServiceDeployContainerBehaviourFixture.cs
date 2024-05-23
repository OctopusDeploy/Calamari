using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Behaviors;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using Octostache;

namespace Calamari.AzureAppService.Tests
{
    /// <summary>
    /// Tests that both windows and linux app services can have container deployments
    /// </summary>
    /// <remarks>
    /// Both test fixtures have the same two tests, but they have different setups, so it's just easier to have separate test fixtures.
    /// </remarks>
    public class AzureAppServiceDeployContainerBehaviourFixture
    {
        [TestFixture]
        public class WhenUsingAWindowsAppService : AppServiceIntegrationTest
        {
            CalamariVariables newVariables;
            readonly HttpClient client = new HttpClient();

            // For some reason we are having issues creating these linux resources on Standard in EastUS
            protected override string DefaultResourceGroupLocation => "westus2";

            protected override async Task ConfigureTestResources(ResourceGroupResource resourceGroup)
            {
                var (_, webSite) = await CreateAppServicePlanAndWebApp(resourceGroup,
                                                                       webSiteData: new WebSiteData(resourceGroup.Data.Location)
                                                                       {
                                                                           SiteConfig = new SiteConfigProperties
                                                                           {
                                                                               WindowsFxVersion = "DOCKER|mcr.microsoft.com/azuredocs/aci-helloworld",
                                                                               IsAlwaysOn = true,
                                                                               AppSettings = new List<AppServiceNameValuePair>
                                                                               {
                                                                                   new AppServiceNameValuePair { Name = "DOCKER_REGISTRY_SERVER_URL", Value = "https://index.docker.io" },
                                                                                   new AppServiceNameValuePair { Name = "WEBSITES_ENABLE_APP_SERVICE_STORAGE", Value = "false" },
                                                                                   new AppServiceNameValuePair { Name = "WEBSITES_CONTAINER_START_TIME_LIMIT", Value = "460" }
                                                                               }
                                                                           }
                                                                       });

                WebSiteResource = webSite;

                await AssertSetupSuccessAsync();
            }

            [Test]
            public async Task AzureWindowsContainerDeploy()
            {
                newVariables = new CalamariVariables();
                AddVariables(newVariables);

                var runningContext = new RunningDeployment("", newVariables);

                await new AzureAppServiceContainerDeployBehaviour(new InMemoryLog()).Execute(runningContext);

                var targetSite = new AzureTargetSite(SubscriptionId,
                                                     ResourceGroupName,
                                                     WebSiteResource.Data.Name);

                await AssertDeploySuccessAsync(targetSite);
            }

            [Test]
            public async Task AzureWindowsContainerSlotDeploy()
            {
                var slotName = "stage";

                newVariables = new CalamariVariables();
                AddVariables(newVariables);
                newVariables.Add("Octopus.Action.Azure.DeploymentSlot", slotName);
                await WebSiteResource.GetWebSiteSlots()
                                     .CreateOrUpdateAsync(WaitUntil.Completed,
                                                          slotName,
                                                          WebSiteResource.Data);

                var runningContext = new RunningDeployment("", newVariables);

                await new AzureAppServiceContainerDeployBehaviour(new InMemoryLog()).Execute(runningContext);

                var targetSite = new AzureTargetSite(SubscriptionId,
                                                     ResourceGroupName,
                                                     WebSiteResource.Data.Name,
                                                     slotName);

                await AssertDeploySuccessAsync(targetSite);
            }

            async Task AssertSetupSuccessAsync()
            {
                var response = await RetryPolicies.TestsTransientHttpErrorsPolicy.ExecuteAsync(async context =>
                                                                                               {
                                                                                                   var r = await client.GetAsync($@"https://{WebSiteResource.Data.DefaultHostName}");

                                                                                                   if (!r.IsSuccessStatusCode)
                                                                                                   {
                                                                                                       var messageContent = await r.Content.ReadAsStringAsync();
                                                                                                       TestContext.WriteLine($"Unable to retrieve content from https://{WebSiteResource.Data.DefaultHostName}, failed with: {messageContent}");
                                                                                                   }

                                                                                                   r.EnsureSuccessStatusCode();
                                                                                                   return r;
                                                                                               },
                                                                                               contextData: new Dictionary<string, object>());

                var receivedContent = await response.Content.ReadAsStringAsync();

                receivedContent.Should().Contain(@"<title>Microsoft Azure App Service - Welcome</title>");
                Assert.IsTrue(response.IsSuccessStatusCode);
            }

            async Task AssertDeploySuccessAsync(AzureTargetSite targetSite)
            {
                var imageName = newVariables.Get(SpecialVariables.Action.Package.PackageId);
                var registryUrl = newVariables.Get(SpecialVariables.Action.Package.Registry);
                var imageVersion = newVariables.Get(SpecialVariables.Action.Package.PackageVersion) ?? "latest";

                var config = await WebSiteResource.GetWebSiteConfig().GetAsync();
                Assert.AreEqual($@"DOCKER|{imageName}:{imageVersion}", config.Value.Data.WindowsFxVersion);

                var appSettings = await ArmClient.GetAppSettingsListAsync(targetSite);
                Assert.AreEqual("https://" + registryUrl, appSettings.FirstOrDefault(app => app.Name == "DOCKER_REGISTRY_SERVER_URL")?.Value);
            }

            void AddVariables(VariableDictionary vars)
            {
                AddAzureVariables(vars);
                vars.Add(SpecialVariables.Action.Package.FeedId, "Feeds-42");
                vars.Add(SpecialVariables.Action.Package.Registry, "index.docker.io");
                vars.Add(SpecialVariables.Action.Package.PackageId, "e2eteam/sample-apiserver");
                vars.Add(SpecialVariables.Action.Package.Image, "e2eteam/sample-apiserver:1.17");
                vars.Add(SpecialVariables.Action.Package.PackageVersion, "1.17");
                vars.Add(SpecialVariables.Action.Azure.DeploymentType, "Container");
            }
        }
        
        [TestFixture]
        public class WhenUsingALinuxAppService : AppServiceIntegrationTest
        {
            CalamariVariables newVariables;
            readonly HttpClient client = new HttpClient();

            // For some reason we are having issues creating these linux resources on Standard in EastUS
            protected override string DefaultResourceGroupLocation => "westus2";

            protected override async Task ConfigureTestResources(ResourceGroupResource resourceGroup)
            {
                var (_, webSite) = await CreateAppServicePlanAndWebApp(resourceGroup,
                                                                       new AppServicePlanData(resourceGroup.Data.Location)
                                                                       {
                                                                           Kind = "linux",
                                                                           IsReserved = true,
                                                                           Sku = new AppServiceSkuDescription
                                                                           {
                                                                               Name = "P1V3",
                                                                               Tier = "PremiumV3"
                                                                           }
                                                                       },
                                                                       new WebSiteData(resourceGroup.Data.Location)
                                                                       {
                                                                           SiteConfig = new SiteConfigProperties
                                                                           {
                                                                               LinuxFxVersion = @"DOCKER|mcr.microsoft.com/azuredocs/aci-helloworld",
                                                                               IsAlwaysOn = true,
                                                                               AppSettings = new List<AppServiceNameValuePair>
                                                                               {
                                                                                   new AppServiceNameValuePair { Name = "DOCKER_REGISTRY_SERVER_URL", Value = "https://index.docker.io" },
                                                                                   new AppServiceNameValuePair { Name = "WEBSITES_ENABLE_APP_SERVICE_STORAGE", Value = "false" },
                                                                                   new AppServiceNameValuePair { Name = "WEBSITES_CONTAINER_START_TIME_LIMIT", Value = "460" }
                                                                               }
                                                                           }
                                                                       });

                WebSiteResource = webSite;

                await AssertSetupSuccessAsync();
            }

            [Test]
            public async Task AzureLinuxContainerDeploy()
            {
                newVariables = new CalamariVariables();
                AddVariables(newVariables);

                var runningContext = new RunningDeployment("", newVariables);

                await new AzureAppServiceContainerDeployBehaviour(new InMemoryLog()).Execute(runningContext);

                var targetSite = new AzureTargetSite(SubscriptionId,
                                                     ResourceGroupName,
                                                     WebSiteResource.Data.Name);

                await AssertDeploySuccessAsync(targetSite);
            }

            [Test]
            public async Task AzureLinuxContainerSlotDeploy()
            {
                var slotName = "stage";

                newVariables = new CalamariVariables();
                AddVariables(newVariables);
                newVariables.Add("Octopus.Action.Azure.DeploymentSlot", slotName);
                await WebSiteResource.GetWebSiteSlots()
                                     .CreateOrUpdateAsync(WaitUntil.Completed,
                                                          slotName,
                                                          WebSiteResource.Data);

                var runningContext = new RunningDeployment("", newVariables);

                await new AzureAppServiceContainerDeployBehaviour(new InMemoryLog()).Execute(runningContext);

                var targetSite = new AzureTargetSite(SubscriptionId,
                                                     ResourceGroupName,
                                                     WebSiteResource.Data.Name,
                                                     slotName);

                await AssertDeploySuccessAsync(targetSite);
            }

            async Task AssertSetupSuccessAsync()
            {
                var response = await RetryPolicies.TestsTransientHttpErrorsPolicy.ExecuteAsync(async context =>
                                                                                               {
                                                                                                   var r = await client.GetAsync($@"https://{WebSiteResource.Data.DefaultHostName}");

                                                                                                   if (!r.IsSuccessStatusCode)
                                                                                                   {
                                                                                                       var messageContent = await r.Content.ReadAsStringAsync();
                                                                                                       TestContext.WriteLine($"Unable to retrieve content from https://{WebSiteResource.Data.DefaultHostName}, failed with: {messageContent}");
                                                                                                   }

                                                                                                   r.EnsureSuccessStatusCode();
                                                                                                   return r;
                                                                                               },
                                                                                               contextData: new Dictionary<string, object>());

                var receivedContent = await response.Content.ReadAsStringAsync();

                receivedContent.Should().Contain(@"<title>Welcome to Azure Container Instances!</title>");
                Assert.IsTrue(response.IsSuccessStatusCode);
            }

            async Task AssertDeploySuccessAsync(AzureTargetSite targetSite)
            {
                var imageName = newVariables.Get(SpecialVariables.Action.Package.PackageId);
                var registryUrl = newVariables.Get(SpecialVariables.Action.Package.Registry);
                var imageVersion = newVariables.Get(SpecialVariables.Action.Package.PackageVersion) ?? "latest";

                var config = await WebSiteResource.GetWebSiteConfig().GetAsync();
                Assert.AreEqual($@"DOCKER|{imageName}:{imageVersion}", config.Value.Data.LinuxFxVersion);

                var appSettings = await ArmClient.GetAppSettingsListAsync(targetSite);
                Assert.AreEqual("https://" + registryUrl, appSettings.FirstOrDefault(app => app.Name == "DOCKER_REGISTRY_SERVER_URL")?.Value);
            }

            void AddVariables(VariableDictionary vars)
            {
                AddAzureVariables(vars);
                vars.Add(SpecialVariables.Action.Package.FeedId, "Feeds-42");
                vars.Add(SpecialVariables.Action.Package.Registry, "index.docker.io");
                vars.Add(SpecialVariables.Action.Package.PackageId, "nginx");
                vars.Add(SpecialVariables.Action.Package.Image, "nginx:latest");
                vars.Add(SpecialVariables.Action.Package.PackageVersion, "latest");
                vars.Add(SpecialVariables.Action.Azure.DeploymentType, "Container");
            }
        }
    }
}