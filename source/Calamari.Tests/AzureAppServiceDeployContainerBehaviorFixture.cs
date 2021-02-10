using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
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
using FluentAssertions;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class AzureAppServiceDeployContainerBehaviorFixture
    {
        private string _clientId;
        private string _clientSecret;
        private string _tenantId;
        private string _subscriptionId;
        private string _webappName;
        private string _resourceGroupName;
        private ResourceGroupsOperations _resourceGroupClient;
        private string _authToken;
        private WebSiteManagementClient _webMgmtClient;
        private CalamariVariables _newVariables;
        readonly HttpClient _client = new HttpClient();
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
            
            _authToken = await GetAuthToken(_tenantId, _clientId, _clientSecret);

            var resourcesClient = new ResourcesManagementClient(_subscriptionId,
                new ClientSecretCredential(_tenantId, _clientId, _clientSecret));

            _resourceGroupClient = resourcesClient.ResourceGroups;

            var resourceGroup = new ResourceGroup(resourceGroupLocation);
            resourceGroup = await _resourceGroupClient.CreateOrUpdateAsync(_resourceGroupName, resourceGroup);

            _webMgmtClient = new WebSiteManagementClient(new TokenCredentials(_authToken))
            {
                SubscriptionId = _subscriptionId,
                HttpClient = { BaseAddress = new Uri(DefaultVariables.ResourceManagementEndpoint) },
            };

            var svcPlan = await _webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(resourceGroup.Name,
                resourceGroup.Name, new AppServicePlan(resourceGroup.Location)
                {
                    Kind = "linux",
                    Reserved = true,
                    Sku = new SkuDescription
                    {
                        Name = "S1",
                        Tier = "Standard"
                    }
                });

            _site = await _webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroup.Name, resourceGroup.Name,
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
                });
            
            _webappName = _site.Name;

            await AssertSetupSuccessAsync();
        }

        [OneTimeTearDown]
        public async Task CleanupCode()
        {
            if(_resourceGroupClient != null)
                await _resourceGroupClient.StartDeleteAsync(_resourceGroupName);

            //foreach (var tempDir in _tempDirs)
            //{
            //    if(tempDir.Exists)
            //        tempDir.Delete(true);
            //}
        }

        [Test]
        public async Task AzureLinuxContainerDeploy()
        {
            _newVariables = new CalamariVariables();
            AddVariables(_newVariables);

            var runningContext = new RunningDeployment("", _newVariables);

            await new AzureAppServiceDeployContainerBehavior(new InMemoryLog()).Execute(runningContext);

            await AssertDeploySuccessAsync(AzureWebAppHelper.GetAzureTargetSite(_site.Name, "", _resourceGroupName));
        }

        [Test]
        public async Task AzureLinuxContainerSlotDeploy()
        {
            var slotName = "stage";
            
            _newVariables = new CalamariVariables();
            AddVariables(_newVariables);
            _newVariables.Add("Octopus.Action.Azure.DeploymentSlot", slotName);
            var slot = await _webMgmtClient.WebApps.BeginCreateOrUpdateSlotAsync(_resourceGroupName, _webappName, _site,
                slotName);

            var runningContext = new RunningDeployment("", _newVariables);

            await new AzureAppServiceDeployContainerBehavior(new InMemoryLog()).Execute(runningContext);

            await AssertDeploySuccessAsync(AzureWebAppHelper.GetAzureTargetSite(_site.Name, slotName, _resourceGroupName));
        }

        async Task AssertSetupSuccessAsync()
        {
            var result = await _client.GetAsync($@"https://{_webappName}.azurewebsites.net");
            var recievedContent = await result.Content.ReadAsStringAsync();

            recievedContent.Should().Contain(@"<title>Welcome to Azure Container Instances!</title>"); 
            Assert.IsTrue(result.IsSuccessStatusCode);
        }

        async Task AssertDeploySuccessAsync(TargetSite targetSite)
        {
            var imageName = _newVariables.Get(SpecialVariables.Action.Package.PackageId);
            var registryUrl = _newVariables.Get(SpecialVariables.Action.Package.FeedId);
            var imageVersion = _newVariables.Get(SpecialVariables.Action.Package.PackageVersion) ?? "latest";

            var config = await _webMgmtClient.WebApps.GetConfigurationAsync(targetSite);
            Assert.AreEqual($@"DOCKER|{imageName}:{imageVersion}", config.LinuxFxVersion);

            var appSettings = await _webMgmtClient.WebApps.ListApplicationSettingsAsync(targetSite);
            Assert.AreEqual(registryUrl, appSettings.Properties["DOCKER_REGISTRY_SERVER_URL"]);
        }

        void AddVariables(CalamariVariables vars)
        {
            vars.Add(AccountVariables.ClientId, _clientId);
            vars.Add(AccountVariables.Password, _clientSecret);
            vars.Add(AccountVariables.TenantId, _tenantId);
            vars.Add(AccountVariables.SubscriptionId, _subscriptionId);
            vars.Add("Octopus.Action.Azure.ResourceGroupName", _resourceGroupName);
            vars.Add("Octopus.Action.Azure.WebAppName", _webappName);
            vars.Add(SpecialVariables.Action.Package.FeedId, "https://index.docker.io");
            vars.Add(SpecialVariables.Action.Package.PackageId, "nginx");
            vars.Add(SpecialVariables.Action.Package.PackageVersion, "latest");
            vars.Add(SpecialVariables.Action.Azure.DeploymentType, "ImageDeploy");
            //vars.Add(SpecialVariables.Action.Azure.ContainerSettings, BuildContainerConfigJson());

        }

        private async Task<string> GetAuthToken(string tenantId, string applicationId, string password)
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ??
                DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ??
                DefaultVariables.ActiveDirectoryEndpoint;

            var authContext = GetContextUri(activeDirectoryEndpointBaseUri, tenantId);
            var context = new AuthenticationContext(authContext);
            var result = await context.AcquireTokenAsync(resourceManagementEndpointBaseUri,
                new ClientCredential(applicationId, password));

            return result.AccessToken;
        }

        string GetContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/"))
            {
                return $"{activeDirectoryEndPoint}/{tenantId}";
            }

            return $"{activeDirectoryEndPoint}{tenantId}";
        }
    }
}
