﻿using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Azure.ResourceManager.Resources.Models;
using Calamari.AzureAppService.Azure;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.LogParser;
using FluentAssertions;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Management.Storage.Models;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using NUnit.Framework;
using FileShare = System.IO.FileShare;
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

            [Test]
            public async Task DeployToTwoTargetsInParallel_Succeeds()
            {
                // Arrange
                var packageInfo = PrepareFunctionAppZipPackage();
                // Without larger changes to Calamari and the Test Framework, it's not possible to run two Calamari
                // processes in parallel in the same test method. Simulate the file locking behaviour by directly
                // opening the affected file instead
                var fileLock = File.Open(packageInfo.packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                try
                {
                    // Act
                    var deployment = await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                        .WithArrange(context =>
                        {
                            context.WithPackage(packageInfo.packagePath, packageInfo.packageName,
                                packageInfo.packageVersion);
                            AddVariables(context);
                            context.Variables[KnownVariables.Package.EnabledFeatures] = null;
                        }).Execute();

                    // Assert
                    deployment.Outcome.Should().Be(TestExecutionOutcome.Successful);
                }
                finally
                {
                    fileLock.Close();
                }
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
            
            private static (string packagePath, string packageName, string packageVersion) PrepareFunctionAppZipPackage()
            {
                (string packagePath, string packageName, string packageVersion) packageInfo;
            
                var testAssemblyLocation = new FileInfo(Assembly.GetExecutingAssembly().Location);
                var sourceZip = Path.Combine(testAssemblyLocation.Directory.FullName, "functionapp.1.0.0.zip");
                
                packageInfo.packagePath = sourceZip;
                packageInfo.packageVersion = "1.0.0";
                packageInfo.packageName = "functionapp";
            
                return packageInfo;
            }

            private void AddVariables(CommandTestBuilderContext context)
            {
                AddAzureVariables(context);
                context.Variables.Add("Greeting", greeting);
                context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles);
                context.Variables.Add(PackageVariables.SubstituteInFilesTargets, "index.html");
                context.Variables.Add(SpecialVariables.Action.Azure.DeploymentType, "ZipDeploy");
            }
        }

        [TestFixture]
        public class WhenUsingALinuxAppService : AppServiceIntegrationTest
        {
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

                site = await webMgmtClient.WebApps.BeginCreateOrUpdateAsync(resourceGroupName,
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
                    await AssertContent($"{site.Name}.azurewebsites.net", 
                        rootPath: $"api/HttpExample?name={greeting}", 
                        actualText: $"Hello, {greeting}");
                },
                secondsBetweenRetries: 10);
            }

            [Test]
            public async Task CanDeployZip_ToLinuxFunctionApp_WithRunFromPackageFlag()
            {
                // Arrange
                var settings = await webMgmtClient.WebApps.ListApplicationSettingsAsync(resourceGroupName, site.Name);
                settings.Properties["WEBSITE_RUN_FROM_PACKAGE"] = "1";
                await webMgmtClient.WebApps.UpdateApplicationSettingsAsync(resourceGroupName, site.Name, settings);
                
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
                        await AssertContent($"{site.Name}.azurewebsites.net", 
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
                AddAzureVariables(context);
                context.Variables.Add(SpecialVariables.Action.Azure.DeploymentType, "ZipDeploy");
            }
        }
    }
}
