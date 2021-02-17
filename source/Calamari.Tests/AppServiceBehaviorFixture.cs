
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.Azure;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class AppServiceBehaviorFixture
    {
        private string _clientId;
        private string _clientSecret;
        private string _tenantId;
        private string _subscriptionId;
        private string _resourceGroupName;
        private string _authToken;
        private string _greeting = "Calamari";
        private ResourceGroupsOperations _resourceGroupClient;
        private WebSiteManagementClient _webMgmtClient;
        private Site _site;
        readonly HttpClient _client = new HttpClient();
        
        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ??
                DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ??
                DefaultVariables.ActiveDirectoryEndpoint;
            
            _resourceGroupName = Guid.NewGuid().ToString();

            _clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            _clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            _tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            _subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            var resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "eastus";

            _authToken = await Auth.GetAuthTokenAsync(activeDirectoryEndpointBaseUri, resourceManagementEndpointBaseUri,
                _tenantId, _clientId, _clientSecret);

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
                resourceGroup.Name,
                new AppServicePlan(resourceGroup.Location) {Sku = new SkuDescription("S1", "Standard")}
            );

            _site = await _webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroup.Name, resourceGroup.Name,
                new Site(resourceGroup.Location) { ServerFarmId = svcPlan.Id });
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            await _resourceGroupClient.StartDeleteAsync(_resourceGroupName);
        }

        [Test]
        public async Task Deploy_WebAppZip()
        {
            (string packagePath, string packageName, string packageVersion) packageinfo;

            var tempPath = TemporaryDirectory.Create();
            new DirectoryInfo(tempPath.DirectoryPath).CreateSubdirectory("AzureZipDeployPackage");
            File.WriteAllText(Path.Combine($"{tempPath.DirectoryPath}/AzureZipDeployPackage", "index.html"),
                "Hello #{Greeting}");
            
            packageinfo.packagePath = $"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.zip";
            packageinfo.packageVersion = "1.0.0";
            packageinfo.packageName = "AzureZipDeployPackage";
            ZipFile.CreateFromDirectory($"{tempPath.DirectoryPath}/AzureZipDeployPackage", packageinfo.packagePath);

            await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
            {
                context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                AddVariables(context);
            }).Execute();

            //await new AzureAppServiceBehaviour(new InMemoryLog()).Execute(runningContext);
            await AssertContent($"{_site.Name}.azurewebsites.net", $"Hello {_greeting}");
        }

        [Test]
        public async Task Deploy_WebAppZipSlot()
        {
            var slotName = "stage";
            _greeting = "stage";

            (string packagePath, string packageName, string packageVersion) packageinfo;

            var slotTask = _webMgmtClient.WebApps.BeginCreateOrUpdateSlotAsync(_resourceGroupName, _resourceGroupName,
                _site,
                slotName);

            var tempPath = TemporaryDirectory.Create();
            new DirectoryInfo(tempPath.DirectoryPath).CreateSubdirectory("AzureZipDeployPackage");
            File.WriteAllText(Path.Combine($"{tempPath.DirectoryPath}/AzureZipDeployPackage", "index.html"),
                "Hello #{Greeting}");
            packageinfo.packagePath = $"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.zip";
            packageinfo.packageVersion = "1.0.0";
            packageinfo.packageName = "AzureZipDeployPackage";
            ZipFile.CreateFromDirectory($"{tempPath.DirectoryPath}/AzureZipDeployPackage", packageinfo.packagePath);

            await slotTask;

            await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
            {
                context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                AddVariables(context);
                context.Variables.Add("Octopus.Action.Azure.DeploymentSlot", slotName);
            }).Execute();

            await AssertContent($"{_site.Name}-{slotName}.azurewebsites.net", $"Hello {_greeting}");
        }

        [Test]
        public async Task Deploy_NugetPackage()
        {
            (string packagePath, string packageName, string packageVersion) packageinfo;
            _greeting = "nuget";

            var tempPath = TemporaryDirectory.Create();
            new DirectoryInfo(tempPath.DirectoryPath).CreateSubdirectory("AzureZipDeployPackage");
            
            var doc = new XDocument(new XElement("package",
                new XAttribute("xmlns", @"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"),
                new XElement("metadata",
                    new XElement("id", "AzureZipDeployPackage"),
                    new XElement("version", "1.0.0"),
                    new XElement("title", "AzureZipDeployPackage"),
                    new XElement("authors","Chris Thomas"),
                    new XElement("description", "Test Package used to test nuget package deployments")
                )
            ));

            await File.WriteAllTextAsync(Path.Combine($"{tempPath.DirectoryPath}/AzureZipDeployPackage", "index.html"),
                "Hello #{Greeting}");
            using (var writer = new XmlTextWriter(
                Path.Combine($"{tempPath.DirectoryPath}/AzureZipDeployPackage", "AzureZipDeployPackage.nuspec"),
                Encoding.UTF8))
            {
                doc.Save(writer);
            }

            packageinfo.packagePath = $"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.nupkg";
            packageinfo.packageVersion = "1.0.0";
            packageinfo.packageName = "AzureZipDeployPackage";
            ZipFile.CreateFromDirectory($"{tempPath.DirectoryPath}/AzureZipDeployPackage", packageinfo.packagePath);

            await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
            {
                context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                AddVariables(context);
            }).Execute();

            //await new AzureAppServiceBehaviour(new InMemoryLog()).Execute(runningContext);
            await AssertContent($"{_site.Name}.azurewebsites.net", $"Hello {_greeting}");
        }

        private void AddVariables(CommandTestBuilderContext context)
        {
            context.Variables.Add(AccountVariables.ClientId, _clientId);
            context.Variables.Add(AccountVariables.Password, _clientSecret);
            context.Variables.Add(AccountVariables.TenantId, _tenantId);
            context.Variables.Add(AccountVariables.SubscriptionId, _subscriptionId);
            context.Variables.Add("Octopus.Action.Azure.ResourceGroupName", _resourceGroupName);
            context.Variables.Add("Octopus.Action.Azure.WebAppName", _site.Name);
            context.Variables.Add("Greeting", _greeting);
            context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles);
            context.Variables.Add(PackageVariables.SubstituteInFilesTargets, "index.html");
            context.Variables.Add(SpecialVariables.Action.Azure.DeploymentType, "ZipDeploy");
        }

        async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var result = await _client.GetStringAsync($"https://{hostName}/{rootPath}");

            result.Should().Be(actualText);
        }
    }
}