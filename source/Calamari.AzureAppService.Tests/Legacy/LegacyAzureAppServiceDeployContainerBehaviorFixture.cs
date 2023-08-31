using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.AzureAppService;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Behaviors;
using Calamari.AzureAppService.Json;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;
using Polly.Retry;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class LegacyAzureAppServiceDeployContainerBehaviorFixture
    {
        private string clientId;
        private string clientSecret;
        private string tenantId;
        private string subscriptionId;
        private string webappName;
        private string resourceGroupName;
        private ResourceGroupsOperations resourceGroupClient;
        private string authToken;
        private WebSiteManagementClient webMgmtClient;
        private CalamariVariables newVariables;
        readonly HttpClient client = new HttpClient();
        private Site site;
        private RetryPolicy retryPolicy;

        [OneTimeSetUp]
        public async Task Setup()
        {
            retryPolicy = RetryPolicyFactory.CreateForHttp429();

            resourceGroupName = Guid.NewGuid().ToString();

            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            // For some reason we are having issues creating these linux resources on Standard in EastUS
            var resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "westus2";

            authToken = await GetAuthToken(tenantId, clientId, clientSecret);

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
                                                                                                                                  new AppServicePlan(resourceGroup.Location)
                                                                                                                                  {
                                                                                                                                      Kind = "linux",
                                                                                                                                      Reserved = true,
                                                                                                                                      Sku = new SkuDescription
                                                                                                                                      {
                                                                                                                                          Name = "S1",
                                                                                                                                          Tier = "Standard"
                                                                                                                                      }
                                                                                                                                  }));

            site = await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroup.Name,
                                                                                                                   resourceGroup.Name,
                                                                                                                   new Site(resourceGroup.Location)
                                                                                                                   {
                                                                                                                       ServerFarmId = svcPlan.Id,
                                                                                                                       SiteConfig = new SiteConfig
                                                                                                                       {
                                                                                                                           LinuxFxVersion = @"DOCKER|mcr.microsoft.com/azuredocs/aci-helloworld",
                                                                                                                           AppSettings = new List<NameValuePair>
                                                                                                                           {
                                                                                                                               new NameValuePair("DOCKER_REGISTRY_SERVER_URL", "https://index.docker.io"),
                                                                                                                               new NameValuePair("WEBSITES_ENABLE_APP_SERVICE_STORAGE", "false")
                                                                                                                           },
                                                                                                                           AlwaysOn = true
                                                                                                                       }
                                                                                                                   }));

            webappName = site.Name;

            await AssertSetupSuccessAsync();
        }

        [OneTimeTearDown]
        public async Task CleanupCode()
        {
            if (resourceGroupClient != null)
                await resourceGroupClient.StartDeleteAsync(resourceGroupName);

            //foreach (var tempDir in _tempDirs)
            //{
            //    if(tempDir.Exists)
            //        tempDir.Delete(true);
            //}
        }

        [Test]
        public async Task AzureLinuxContainerDeploy()
        {
            newVariables = new CalamariVariables();
            AddVariables(newVariables);

            var runningContext = new RunningDeployment("", newVariables);

            await new LegacyAzureAppServiceDeployContainerBehavior(new InMemoryLog()).Execute(runningContext);

            var targetSite = new AzureTargetSite(subscriptionId, resourceGroupName, site.Name);
            await AssertDeploySuccessAsync(targetSite);
        }

        [Test]
        public async Task AzureLinuxContainerSlotDeploy()
        {
            var slotName = "stage";

            newVariables = new CalamariVariables();
            AddVariables(newVariables);
            newVariables.Add("Octopus.Action.Azure.DeploymentSlot", slotName);
            await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.BeginCreateOrUpdateSlotAsync(resourceGroupName,
                                                                                                                webappName,
                                                                                                                site,
                                                                                                                slotName));

            var runningContext = new RunningDeployment("", newVariables);

            await new LegacyAzureAppServiceDeployContainerBehavior(new InMemoryLog()).Execute(runningContext);

            var targetSite = new AzureTargetSite(subscriptionId, resourceGroupName, site.Name, slotName);
            await AssertDeploySuccessAsync(targetSite);
        }

        async Task AssertSetupSuccessAsync()
        {
            var response = await RetryPolicies.TransientHttpErrorsPolicy.ExecuteAsync(async () =>
                                                                                      {
                                                                                          var r = await client.GetAsync($@"https://{site.DefaultHostName}");
                                                                                          r.EnsureSuccessStatusCode();
                                                                                          return r;
                                                                                      });
            
            var receivedContent = await response.Content.ReadAsStringAsync();

            receivedContent.Should().Contain(@"<title>Welcome to Azure Container Instances!</title>");
            Assert.IsTrue(response.IsSuccessStatusCode);
        }

        async Task AssertDeploySuccessAsync(AzureTargetSite targetSite)
        {
            var imageName = newVariables.Get(SpecialVariables.Action.Package.PackageId);
            var registryUrl = newVariables.Get(SpecialVariables.Action.Package.Registry);
            var imageVersion = newVariables.Get(SpecialVariables.Action.Package.PackageVersion) ?? "latest";

            var config = await webMgmtClient.WebApps.GetConfigurationAsync(targetSite);
            Assert.AreEqual($@"DOCKER|{imageName}:{imageVersion}", config.LinuxFxVersion);

            var appSettings = await webMgmtClient.WebApps.ListApplicationSettingsAsync(targetSite);
            Assert.AreEqual("https://" + registryUrl, appSettings.Properties["DOCKER_REGISTRY_SERVER_URL"]);
        }

        void AddVariables(CalamariVariables vars)
        {
            vars.Add(AccountVariables.ClientId, clientId);
            vars.Add(AccountVariables.Password, clientSecret);
            vars.Add(AccountVariables.TenantId, tenantId);
            vars.Add(AccountVariables.SubscriptionId, subscriptionId);
            vars.Add("Octopus.Action.Azure.ResourceGroupName", resourceGroupName);
            vars.Add("Octopus.Action.Azure.WebAppName", webappName);
            vars.Add(SpecialVariables.Action.Package.FeedId, "Feeds-42");
            vars.Add(SpecialVariables.Action.Package.Registry, "index.docker.io");
            vars.Add(SpecialVariables.Action.Package.PackageId, "nginx");
            vars.Add(SpecialVariables.Action.Package.Image, "nginx:latest");
            vars.Add(SpecialVariables.Action.Package.PackageVersion, "latest");
            vars.Add(SpecialVariables.Action.Azure.DeploymentType, "Container");
            //vars.Add(SpecialVariables.Action.Azure.ContainerSettings, BuildContainerConfigJson());
        }

        private async Task<string> GetAuthToken(string tenantId, string applicationId, string password)
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            return await Auth.GetServicePrincipalAuthTokenAsync(tenantId,
                                                                applicationId,
                                                                password,
                                                                resourceManagementEndpointBaseUri,
                                                                activeDirectoryEndpointBaseUri);
        }
    }
}