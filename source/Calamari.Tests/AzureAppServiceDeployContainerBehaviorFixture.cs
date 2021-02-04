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
        private const string _slotName = "stage";
        private string _resourceGroupName;
        private ResourceGroupsOperations _resourceGroupClient;
        private IList<DirectoryInfo> _tempDirs;
        private string _authToken;
        private WebSiteManagementClient _webMgmtClient;

        readonly HttpClient client = new HttpClient();

        [OneTimeSetUp]
        public async Task Setup()
        {
            _resourceGroupName = Guid.NewGuid().ToString();
            _tempDirs = new List<DirectoryInfo>();

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
                        Name = "B1",
                        Tier = "Basic"
                    }
                });

            var webapp = await _webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroup.Name, resourceGroup.Name,
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

            //var slot =
            //    await _webMgmtClient.WebApps.BeginCreateOrUpdateSlotAsync(resourceGroup.Name, webapp.Name, webapp,
            //        "stage");
            
            _webappName = webapp.Name;

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
            var iVars = new CalamariVariables();
            AddVariables(iVars);

            var runningContext = new RunningDeployment("", iVars);

            await new AzureAppServiceDeployContainerBehavior(new InMemoryLog()).Execute(runningContext);

            await AssertDeploySuccessAsync();
        }

        async Task AssertSetupSuccessAsync()
        {
            var result = await client.GetAsync($@"https://{_webappName}.azurewebsites.net");
            var recievedContent = await result.Content.ReadAsStringAsync();

            recievedContent.Should().Contain(@"<title>Welcome to Azure Container Instances!</title>"); 
            Assert.IsTrue(result.IsSuccessStatusCode);
        }

        async Task AssertDeploySuccessAsync()
        {
            var result = await client.GetAsync($@"https://{_webappName}.azurewebsites.net");
            var recievedContent = await result.Content.ReadAsStringAsync();

            recievedContent.Should().Contain(@"<title>Welcome to nginx!</title>");
            Assert.IsTrue(result.IsSuccessStatusCode);
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
            //vars.Add(SpecialVariables.Action.Azure.ContainerSettings, BuildContainerConfigJson());

        }

        string BuildContainerConfigJson()
        {
            var containerSettings = new ContainerSettings
            {
                IsEnabled = true,
                ImageName = "nginx",
                ImageTag = "latest",
                RegistryUrl = @"https://index.docker.io"
            };

            var jsonString = JsonConvert.SerializeObject(containerSettings);
            return jsonString;
        }

        private async Task<string> GetAuthToken(string tenantId, string applicationId, string password)
        {
            var activeDirectoryEndPoint = @"https://login.windows.net/";
            var managementEndPoint = @"https://management.azure.com/";
            var authContext = GetContextUri(activeDirectoryEndPoint, tenantId);
            var context = new AuthenticationContext(authContext);
            var result = await context.AcquireTokenAsync(managementEndPoint,
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
