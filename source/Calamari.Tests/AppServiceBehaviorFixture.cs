using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
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
using Calamari.Tests.Shared.LogParser;
using FluentAssertions;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Management.Storage.Models;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using NUnit.Framework;
using Sku = Microsoft.Azure.Management.Storage.Models.Sku;
using StorageManagementClient = Microsoft.Azure.Management.Storage.StorageManagementClient;

namespace Calamari.AzureAppService.Tests
{
    public class AppServiceBehaviorFixture
    {
        [TestFixture]
        public class WhenUsingAWindowsDotNetAppService : AppServiceIntegrationTest
        {
            private string servicePlanId;
            protected override async Task ConfigureTestResources(ResourceGroup resourceGroup)
            {
                var svcPlan = await webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(
                    resourceGroupName: resourceGroup.Name,
                    name: resourceGroup.Name,
                    new AppServicePlan(resourceGroup.Location)
                    {
                        Sku = new SkuDescription("S1", "Standard")
                    }
                );

                servicePlanId = svcPlan.Id;
                
                site = await webMgmtClient.WebApps.BeginCreateOrUpdateAsync(
                    resourceGroupName: resourceGroup.Name,
                    name: resourceGroup.Name,
                    new Site(resourceGroup.Location)
                    {
                        ServerFarmId = svcPlan.Id
                    }
                );
            }

            [Test]
            public async Task CanDeployWebAppZip()
            {
                var packageInfo = PrepareZipPackage();
                
                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
                {
                    context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                    AddVariables(context);
                }).Execute();

                await AssertContent($"{site.Name}.azurewebsites.net", $"Hello {greeting}");
            }
            
            [Test]
            public async Task CanDeployWebAppZip_WithAzureCloudEnvironment()
            {
                var packageinfo = PrepareZipPackage();

                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
                {
                    context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                    AddVariables(context);
                    context.AddVariable(AccountVariables.Environment, "AzureCloud");
                }).Execute();
                
                await AssertContent($"{site.Name}.azurewebsites.net", $"Hello {greeting}");
            }
            
            [Test]
            public async Task CanDeployWebAppZip_ToDeploymentSlot()
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
            public async Task CanDeployNugetPackage()
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

                await Task.Run(() => File.WriteAllText(
                    Path.Combine($"{tempPath.DirectoryPath}/AzureZipDeployPackage", "index.html"),
                    "Hello #{Greeting}"));

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
            public async Task CanDeployWarPackage()
            {
                // Need to spin up a specific app service with Tomcat installed
                // Need java installed on the test runner (MJH 2022-05-06: is this actually true? I don't see why we'd need java on the test runner)
                var javaSite = await webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroupName,
                    $"{resourceGroupName}-java", new Site(site.Location)
                    {
                        ServerFarmId = servicePlanId,
                        SiteConfig = new SiteConfig
                        {
                            JavaVersion = "1.8",
                            JavaContainer = "TOMCAT",
                            JavaContainerVersion = "9.0"
                        }
                    });


                (string packagePath, string packageName, string packageVersion) packageinfo;
                var assemblyFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
                packageinfo.packagePath = Path.Combine(assemblyFileInfo.Directory.FullName, "sample.1.0.0.war");
                packageinfo.packageVersion = "1.0.0";
                packageinfo.packageName = "sample";
                greeting = "java";

                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
                {
                    context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                    AddVariables(context);
                    context.Variables["Octopus.Action.Azure.WebAppName"] = javaSite.Name;
                    context.Variables[PackageVariables.SubstituteInFilesTargets] = "test.jsp";
                }).Execute();

                await AssertContent($"{javaSite.Name}.azurewebsites.net", $"Hello! {greeting}", "test.jsp");
            }
            
            [Test]
            public async Task DeployingWithInvalidEnvironment_ThrowsAnException()
            {
                var packageinfo = PrepareZipPackage();
                
                var commandResult = await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
                    {
                        context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                        AddVariables(context);
                        context.AddVariable(AccountVariables.Environment, "NonSenseEnvironment");
                    }).Execute(false);

                commandResult.Outcome.Should().Be(TestExecutionOutcome.Unsuccessful);
            }

            private static (string packagePath, string packageName, string packageVersion) PrepareZipPackage()
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
                return packageinfo;
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
        }

        [TestFixture]
        public class WhenUsingALinuxAppService : AppServiceIntegrationTest
        {
            private string linuxServicePlanName;
            private string functionAppSiteName;
            
            protected override async Task ConfigureTestResources(ResourceGroup resourceGroup)
            {
                var storageClient = new StorageManagementClient(new TokenCredentials(authToken))
                {
                    SubscriptionId = subscriptionId
                };
                var storageAccountName = resourceGroupName.Replace("-", "").Substring(0, 20);
                var storageAccount = await storageClient.StorageAccounts.CreateAsync(resourceGroupName,
                    accountName: storageAccountName,
                    new StorageAccountCreateParameters()
                    {
                        Sku = new Sku("Standard_LRS"),
                        Kind = "Storage",
                        Location = resourceGroupLocation
                    }
                );
                
                var keys = await storageClient.StorageAccounts.ListKeysAsync(resourceGroupName, storageAccountName);
                
                var linuxSvcPlan = await webMgmtClient.AppServicePlans.BeginCreateOrUpdateAsync(resourceGroupName,
                    $"{resourceGroupName}-linux-asp",
                    new AppServicePlan(resourceGroupLocation)
                    {
                        Sku = new SkuDescription("B1", "Basic"), 
                        Kind = "linux", 
                        Reserved = true
                    }
                );

                var functionAppSite = await webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroupName,
                    $"{resourceGroupName}-linux",
                    new Site(resourceGroupLocation)
                    {
                        ServerFarmId = linuxSvcPlan.Id, 
                        Kind = "functionapp,linux", 
                        Reserved = true, 
                        SiteConfig = new SiteConfig
                        {
                            AlwaysOn = true,
                            LinuxFxVersion = "DOTNET|6.0",
                            Use32BitWorkerProcess = true,
                            AppSettings = new List<NameValuePair>
                            {
                                new NameValuePair("FUNCTIONS_WORKER_RUNTIME", "dotnet"),
                                new NameValuePair("FUNCTIONS_EXTENSION_VERSION", "~4"),
                                new NameValuePair("AzureWebJobsStorage", $"DefaultEndpointsProtocol=https;AccountName={storageAccount.Name};AccountKey={keys.Keys.First().Value};EndpointSuffix=core.windows.net")
                            }
                        }
                    }
                );
                
                linuxServicePlanName = linuxSvcPlan.Name;
                functionAppSiteName = functionAppSite.Name;
            }
            
            [Test]
            public async Task CanDeployZip_ToLinuxFunctionApp()
            {
                // Arrange
                var packageInfo = PrepareZipPackage();

                // Act
                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
                {
                    context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                    AddVariables(context);
                }).Execute();
                
                // Assert
                await DoWithRetries(10, async () =>
                {
                    await AssertContent($"{functionAppSiteName}.azurewebsites.net", 
                        rootPath: $"api/HttpExample?name={greeting}", 
                        actualText: $"Hello, {greeting}");
                },
                secondsBetweenRetries: 10);
            }

            [Test]
            public async Task CanDeployZip_ToLinuxFunctionApp_WithRunFromPackageFlag()
            {
                // Arrange
                var settings = await webMgmtClient.WebApps.ListApplicationSettingsAsync(resourceGroupName, functionAppSiteName);
                settings.Properties["WEBSITE_RUN_FROM_PACKAGE"] = "1";
                await webMgmtClient.WebApps.UpdateApplicationSettingsAsync(resourceGroupName, functionAppSiteName, settings);
                
                var packageInfo = PrepareZipPackage();

                // Act
                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>().WithArrange(context =>
                {
                    context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                    AddVariables(context);
                }).Execute();
                
                // Assert
                await DoWithRetries(10, async () =>
                    {
                        await AssertContent($"{functionAppSiteName}.azurewebsites.net", 
                            rootPath: $"api/HttpExample?name={greeting}", 
                            actualText: $"Hello, {greeting}");
                    },
                    secondsBetweenRetries: 10);
            }

            private static (string packagePath, string packageName, string packageVersion) PrepareZipPackage()
            {
                // Looks like there's some file locking issues if multiple tests try to copy from the same file when running in parallel.
                // For each test that needs one, create a temporary copy.
                (string packagePath, string packageName, string packageVersion) packageInfo;
                
                var tempPath = TemporaryDirectory.Create();
                new DirectoryInfo(tempPath.DirectoryPath).CreateSubdirectory("AzureZipDeployPackage");
                
                var testAssemblyLocation = new FileInfo(Assembly.GetExecutingAssembly().Location);
                var sourceZip = Path.Combine(testAssemblyLocation.Directory.FullName, "functionapp.1.0.0.zip");
                var temporaryZipLocationForTest = $"{tempPath.DirectoryPath}/functionapp.1.0.0.zip";
                File.Copy(sourceZip, temporaryZipLocationForTest);

                packageInfo.packagePath = temporaryZipLocationForTest;
                packageInfo.packageVersion = "1.0.0";
                packageInfo.packageName = "functionapp";
                
                return packageInfo;
            }

            private void AddVariables(CommandTestBuilderContext context)
            {
                context.Variables.Add(AccountVariables.ClientId, clientId);
                context.Variables.Add(AccountVariables.Password, clientSecret);
                context.Variables.Add(AccountVariables.TenantId, tenantId);
                context.Variables.Add(AccountVariables.SubscriptionId, subscriptionId);
                context.Variables.Add("Octopus.Action.Azure.ResourceGroupName", resourceGroupName);
                context.Variables.Add("Octopus.Action.Azure.WebAppName", functionAppSiteName);
                context.Variables.Add(SpecialVariables.Action.Azure.DeploymentType, "ZipDeploy");
            }

            private static async Task DoWithRetries(int retries, Func<Task> action, int secondsBetweenRetries)
            {
                foreach (var retry in Enumerable.Range(1, retries))
                {
                    try
                    {
                        await action();
                    }
                    catch
                    {
                        if (retry == retries)
                            throw;
                        
                        await Task.Delay(secondsBetweenRetries * 1000);
                    }
                }
            }
        }
    }

    public abstract class AppServiceIntegrationTest
    {
        protected string clientId;
        protected string clientSecret;
        protected string tenantId;
        protected string subscriptionId;
        protected string resourceGroupName;
        protected string resourceGroupLocation;
        protected string greeting = "Calamari";
        protected string authToken;
        protected WebSiteManagementClient webMgmtClient;
        protected Site site;

        private ResourceGroupsOperations resourceGroupClient;
        private readonly HttpClient client = new HttpClient();
        
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
            resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? "eastus";

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

            await ConfigureTestResources(resourceGroup);
        }

        protected abstract Task ConfigureTestResources(ResourceGroup resourceGroup);
        
        [OneTimeTearDown]
        public async Task Cleanup()
        {
            await resourceGroupClient.StartDeleteAsync(resourceGroupName);
        }
        
        protected async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var result = await client.GetStringAsync($"https://{hostName}/{rootPath}");

            result.Should().Contain(actualText);
        }
    }
}
