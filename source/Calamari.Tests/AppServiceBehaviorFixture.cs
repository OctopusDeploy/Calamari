
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
        private string clientId;
        private string clientSecret;
        private string tenantId;
        private string subscriptionId;
        private string resourceGroupName;
        private string authToken;
        private string greeting = "Calamari";
        private ResourceGroupsOperations resourceGroupClient;
        private WebSiteManagementClient webMgmtClient;
        private Site site;
        readonly HttpClient client = new HttpClient();
        
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

            authToken = await Auth.GetAuthTokenAsync(activeDirectoryEndpointBaseUri, resourceManagementEndpointBaseUri,
                tenantId, clientId, clientSecret);

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

            var svcPlan = await webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(resourceGroup.Name,
                resourceGroup.Name,
                new AppServicePlan(resourceGroup.Location) {Sku = new SkuDescription("S1", "Standard")}
            );

            site = await webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroup.Name, resourceGroup.Name,
                new Site(resourceGroup.Location) { ServerFarmId = svcPlan.Id });
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            await resourceGroupClient.StartDeleteAsync(resourceGroupName);
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
            await AssertContent($"{site.Name}.azurewebsites.net", $"Hello {greeting}");
        }

        [Test]
        public async Task Deploy_WebAppZipSlot()
        {
            var slotName = "stage";
            greeting = "stage";

            (string packagePath, string packageName, string packageVersion) packageinfo;

            var slotTask = webMgmtClient.WebApps.BeginCreateOrUpdateSlotAsync(resourceGroupName, resourceGroupName,
                site,
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

            await AssertContent($"{site.Name}-{slotName}.azurewebsites.net", $"Hello {greeting}");
        }

        [Test]
        public async Task Deploy_NugetPackage()
        {
            (string packagePath, string packageName, string packageVersion) packageinfo;
            greeting = "nuget";

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
            await AssertContent($"{site.Name}.azurewebsites.net", $"Hello {greeting}");
        }

        [Test]
        public async Task Deploy_WarPackage()
        {
            // need to spin up java app service plan with a tomcat server
            // need java installed on the test runner
            // var javaSvcPlan = await _webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(_resourceGroupName, $"{_resourceGroupName}-java",new AppServicePlan(_site.Location){})
            // var javaSite = await _webMgmtClient.WebApps.BeginCreateOrUpdateAsync(_resourceGroupName, $"{_resourceGroupName}-java",new Site(_site.Location){ServerFarmId = })
        }

        [Test]
        public async Task Deploy_WarPackage()
        {
            // need to spin up java app service plan with a tomcat server
            // need java installed on the test runner
            // var javaSvcPlan = await _webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(_resourceGroupName, $"{_resourceGroupName}-java",new AppServicePlan(_site.Location){})
            // var javaSite = await _webMgmtClient.WebApps.BeginCreateOrUpdateAsync(_resourceGroupName, $"{_resourceGroupName}-java",new Site(_site.Location){ServerFarmId = })
        }

        private void AddVariables(CommandTestBuilderContext context)
        {
            context.Variables.Add(AccountVariables.ClientId, clientId);
            context.Variables.Add(AccountVariables.Password, clientSecret);
            context.Variables.Add(AccountVariables.TenantId, tenantId);
            context.Variables.Add(AccountVariables.SubscriptionId, subscriptionId);
            context.Variables.Add("Octopus.Action.Azure.ResourceGroupName", resourceGroupName);
            context.Variables.Add("Octopus.Action.Azure.WebAppName", site.Name);
            context.Variables.Add("Greeting", greeting);
            context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles);
            context.Variables.Add(PackageVariables.SubstituteInFilesTargets, "index.html");
            context.Variables.Add(SpecialVariables.Action.Azure.DeploymentType, "ZipDeploy");
        }

        async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var result = await client.GetStringAsync($"https://{hostName}/{rootPath}");

            result.Should().Be(actualText);
        }
    }
}