using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Azure;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Calamari.AzureAppService.Azure;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.LogParser;
using FluentAssertions;
using NUnit.Framework;
using FileShare = System.IO.FileShare;

namespace Calamari.AzureAppService.Tests
{
    public class AppServiceBehaviorFixture
    {
        [TestFixture]
        public class WhenUsingAWindowsDotNetAppService : AppServiceIntegrationTest
        {
            private AppServicePlanResource appServicePlanResource;

            protected override async Task ConfigureTestResources(ResourceGroupResource resourceGroup)
            {
                var (appServicePlan, webSite) = await CreateAppServicePlanAndWebApp(resourceGroup);

                appServicePlanResource = appServicePlan;
                WebSiteResource = webSite;
            }

            [Test]
            public async Task CanDeployWebAppZip()
            {
                var packageInfo = PrepareZipPackage();

                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                                                         AddVariables(context);
                                                     })
                                        .Execute();

                await AssertContent(WebSiteResource.Data.DefaultHostName, $"Hello {greeting}");
            }

            [Test]
            public async Task CanDeployWebAppZip_WithAzureCloudEnvironment()
            {
                var packageinfo = PrepareZipPackage();

                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                                                         AddVariables(context);
                                                         context.AddVariable(AccountVariables.Environment, "AzureCloud");
                                                     })
                                        .Execute();

                await AssertContent(WebSiteResource.Data.DefaultHostName, $"Hello {greeting}");
            }

            [Test]
            public async Task CanDeployWebAppZip_WithAsyncDeploymentAndPolling()
            {
                var packageInfo = PrepareZipPackage();

                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                                                         AddVariables(context);

                                                         var existingFeatureToggles = context.Variables.GetStrings(KnownVariables.EnabledFeatureToggles);
                                                         context.Variables.SetStrings(KnownVariables.EnabledFeatureToggles,
                                                                                      existingFeatureToggles.Concat(new[]
                                                                                      {
                                                                                          FeatureToggle.AsynchronousAzureZipDeployFeatureToggle.ToString()
                                                                                      }));
                                                     })
                                        .Execute();

                await AssertContent(WebSiteResource.Data.DefaultHostName, $"Hello {greeting}");
            }

            [Test]
            public async Task CanDeployWebAppZip_ToDeploymentSlot()
            {
                const string slotName = "stage";
                greeting = "stage";

                (string packagePath, string packageName, string packageVersion) packageinfo;

                var slotTask = WebSiteResource.GetWebSiteSlots()
                                              .CreateOrUpdateAsync(WaitUntil.Completed, slotName, WebSiteResource.Data);

                var tempPath = TemporaryDirectory.Create();
                new DirectoryInfo(tempPath.DirectoryPath).CreateSubdirectory("AzureZipDeployPackage");
                File.WriteAllText(Path.Combine($"{tempPath.DirectoryPath}/AzureZipDeployPackage", "index.html"),
                                  "Hello #{Greeting}");
                packageinfo.packagePath = $"{tempPath.DirectoryPath}/AzureZipDeployPackage.1.0.0.zip";
                packageinfo.packageVersion = "1.0.0";
                packageinfo.packageName = "AzureZipDeployPackage";
                ZipFile.CreateFromDirectory($"{tempPath.DirectoryPath}/AzureZipDeployPackage", packageinfo.packagePath);

                var slotResponse = await slotTask;
                var slotResource = slotResponse.Value;

                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                                                         AddVariables(context);
                                                         context.Variables.Add("Octopus.Action.Azure.DeploymentSlot", slotName);
                                                     })
                                        .Execute();

                await AssertContent(slotResource.Data.DefaultHostName, $"Hello {greeting}");
            }

            [Test]
            public async Task CanDeployNugetPackage()
            {
                var packageInfo = await PrepareNugetPackage();

                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                                                         AddVariables(context);
                                                     })
                                        .Execute();

                //await new AzureAppServiceBehaviour(new InMemoryLog()).Execute(runningContext);
                await AssertContent(WebSiteResource.Data.DefaultHostName, $"Hello {greeting}");
            }

            [Test]
            public async Task CanDeployNugetPackage_WithAsyncDeploymentAndPolling()
            {
                var packageInfo = await PrepareNugetPackage();

                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                                                         AddVariables(context);

                                                         var existingFeatureToggles = context.Variables.GetStrings(KnownVariables.EnabledFeatureToggles);
                                                         context.Variables.SetStrings(KnownVariables.EnabledFeatureToggles,
                                                                                      existingFeatureToggles.Concat(new[]
                                                                                      {
                                                                                          FeatureToggle.AsynchronousAzureZipDeployFeatureToggle.ToString()
                                                                                      }));
                                                     })
                                        .Execute();

                //await new AzureAppServiceBehaviour(new InMemoryLog()).Execute(runningContext);
                await AssertContent(WebSiteResource.Data.DefaultHostName, $"Hello {greeting}");
            }

            [Test]
            public async Task CanDeployWarPackage()
            {
                // Need to spin up a specific app service with Tomcat installed
                // Need java installed on the test runner (MJH 2022-05-06: is this actually true? I don't see why we'd need java on the test runner)
                var javaSite = await ResourceGroupResource.GetWebSites()
                                                          .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                               $"{ResourceGroupName}-java",
                                                                               new WebSiteData(ResourceGroupResource.Data.Location)
                                                                               {
                                                                                   AppServicePlanId = appServicePlanResource.Data.Id,
                                                                                   SiteConfig = new SiteConfigProperties
                                                                                   {
                                                                                       JavaVersion = "1.8",
                                                                                       JavaContainer = "TOMCAT",
                                                                                       JavaContainerVersion = "9.0",
                                                                                       AppSettings = new List<AppServiceNameValuePair>
                                                                                       {
                                                                                           new AppServiceNameValuePair { Name = "WEBSITES_CONTAINER_START_TIME_LIMIT", Value = "600" },
                                                                                           new AppServiceNameValuePair { Name = "WEBSITE_SCM_ALWAYS_ON_ENABLED", Value = "true" }
                                                                                       }
                                                                                   }
                                                                               });

                (string packagePath, string packageName, string packageVersion) packageinfo;
                var assemblyFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
                packageinfo.packagePath = Path.Combine(assemblyFileInfo.Directory.FullName, "sample.1.0.0.war");
                packageinfo.packageVersion = "1.0.0";
                packageinfo.packageName = "sample";
                greeting = "java";

                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                                                         AddVariables(context);
                                                         context.Variables[SpecialVariables.Action.Azure.WebAppName] = javaSite.Value.Data.Name;
                                                         context.Variables[PackageVariables.SubstituteInFilesTargets] = "test.jsp";
                                                     })
                                        .Execute();
                
                await DoWithRetries(3,
                                    async () =>
                                    {
                                        await AssertContent(javaSite.Value.Data.DefaultHostName, $"Hello! {greeting}", "test.jsp");
                                    },
                                    secondsBetweenRetries: 10);
            }

            [Test]
            public async Task DeployingWithInvalidEnvironment_ThrowsAnException()
            {
                var packageinfo = PrepareZipPackage();

                var commandResult = await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                                            .WithArrange(context =>
                                                                         {
                                                                             context.WithPackage(packageinfo.packagePath, packageinfo.packageName, packageinfo.packageVersion);
                                                                             AddVariables(context);
                                                                             context.AddVariable(AccountVariables.Environment, "NonSenseEnvironment");
                                                                         })
                                                            .Execute(false);

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
                                                                              context.WithPackage(packageInfo.packagePath,
                                                                                                  packageInfo.packageName,
                                                                                                  packageInfo.packageVersion);
                                                                              AddVariables(context);
                                                                              context.Variables[KnownVariables.Package.EnabledFeatures] = null;
                                                                          })
                                                             .Execute();

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


            async Task<(string packagePath, string packageName, string packageVersion)> PrepareNugetPackage()
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
                                                                  new XElement("authors", "Chris Thomas"),
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
                
                var settings = BuildAppSettingsJson(new[]
                {
                    ("WEBSITES_CONTAINER_START_TIME_LIMIT", "460", false),
                    ("WEBSITE_SCM_ALWAYS_ON_ENABLED", "true", false)
                });
                
                context.Variables[SpecialVariables.Action.Azure.AppSettings] =  settings.json;
            }
        }

        [TestFixture]
        public class WhenUsingALinuxAppService : AppServiceIntegrationTest
        {
            // For some reason we are having issues creating these linux resources on Standard in EastUS
            protected override string DefaultResourceGroupLocation => "westus2";
            static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
            readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

            protected override async Task ConfigureTestResources(ResourceGroupResource resourceGroup)
            {
                var storageAccountName = ResourceGroupName.Replace("-", "").Substring(0, 20);

                var storageAccountResponse = await ResourceGroupResource
                                                   .GetStorageAccounts()
                                                   .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                        storageAccountName,
                                                                        new StorageAccountCreateOrUpdateContent(
                                                                                                                new StorageSku(StorageSkuName.StandardLrs),
                                                                                                                StorageKind.Storage,
                                                                                                                ResourceGroupResource.Data.Location)
                                                                       );

                var keys = await storageAccountResponse
                                 .Value
                                 .GetKeysAsync()
                                 .ToListAsync();

                var linuxAppServicePlan = await resourceGroup.GetAppServicePlans()
                                                             .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                                  $"{resourceGroup.Data.Name}-linux-asp",
                                                                                  new AppServicePlanData(resourceGroup.Data.Location)
                                                                                  {
                                                                                      Sku = new AppServiceSkuDescription
                                                                                      {
                                                                                          Name = "P1V3",
                                                                                          Tier = "PremiumV3"
                                                                                      },
                                                                                      Kind = "linux",
                                                                                      IsReserved = true
                                                                                  });

                await linuxAppServicePlan.WaitForCompletionAsync(cancellationToken);

                var linuxWebSiteResponse = await resourceGroup.GetWebSites()
                                                              .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                                   $"{resourceGroup.Data.Name}-linux",
                                                                                   new WebSiteData(resourceGroup.Data.Location)
                                                                                   {
                                                                                       AppServicePlanId = linuxAppServicePlan.Value.Id,
                                                                                       Kind = "functionapp,linux",
                                                                                       IsReserved = true,
                                                                                       SiteConfig = new SiteConfigProperties
                                                                                       {
                                                                                           IsAlwaysOn = true,
                                                                                           LinuxFxVersion = "DOTNET|6.0",
                                                                                           Use32BitWorkerProcess = true,
                                                                                           AppSettings = new List<AppServiceNameValuePair>
                                                                                           {
                                                                                               new AppServiceNameValuePair { Name = "FUNCTIONS_WORKER_RUNTIME", Value = "dotnet" },
                                                                                               new AppServiceNameValuePair { Name = "FUNCTIONS_EXTENSION_VERSION", Value = "~4" },
                                                                                               new AppServiceNameValuePair { Name = "AzureWebJobsStorage", Value = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={keys.First().Value};EndpointSuffix=core.windows.net" },
                                                                                               new AppServiceNameValuePair { Name = "WEBSITES_CONTAINER_START_TIME_LIMIT", Value = "460" },
                                                                                               new AppServiceNameValuePair { Name = "WEBSITE_SCM_ALWAYS_ON_ENABLED", Value = "true"}
                                                                                           }
                                                                                       }
                                                                                   });

                await linuxWebSiteResponse.WaitForCompletionAsync(cancellationToken);
                
                WebSiteResource = linuxWebSiteResponse.Value;
            }

            [Test]
            public async Task CanDeployZip_ToLinuxFunctionApp()
            {
                // Arrange
                var packageInfo = PrepareZipPackage();

                // Act
                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                                                         AddVariables(context);
                                                     })
                                        .Execute();

                // Assert
                await DoWithRetries(2,
                                    async () =>
                                    {
                                        await AssertContent(WebSiteResource.Data.DefaultHostName,
                                                            rootPath: $"api/HttpExample?name={greeting}",
                                                            actualText: $"Hello, {greeting}");
                                    },
                                    secondsBetweenRetries: 10);
            }

            [Test]
            public async Task CanDeployZip_ToLinuxFunctionApp_WithRunFromPackageFlag()
            {
                // Arrange
                AppServiceConfigurationDictionary settings = await WebSiteResource.GetApplicationSettingsAsync();
                settings.Properties["WEBSITE_RUN_FROM_PACKAGE"] = "1";
                await WebSiteResource.UpdateApplicationSettingsAsync(settings);

                var packageInfo = PrepareZipPackage();

                // Act
                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                                                         AddVariables(context);
                                                     })
                                        .Execute();

                // Assert
                await DoWithRetries(2,
                                    async () =>
                                    {
                                        await AssertContent(WebSiteResource.Data.DefaultHostName,
                                                            rootPath: $"api/HttpExample?name={greeting}",
                                                            actualText: $"Hello, {greeting}");
                                    },
                                    secondsBetweenRetries: 10);
            }


            [Test]
            public async Task CanDeployZip_ToLinuxFunctionApp_WithAsyncDeploymentAndPolling()
            {
                // Arrange
                var packageInfo = PrepareZipPackage();

                // Act
                await CommandTestBuilder.CreateAsync<DeployAzureAppServiceCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.WithPackage(packageInfo.packagePath, packageInfo.packageName, packageInfo.packageVersion);
                                                         AddVariables(context);

                                                         var existingFeatureToggles = context.Variables.GetStrings(KnownVariables.EnabledFeatureToggles);
                                                         context.Variables.SetStrings(KnownVariables.EnabledFeatureToggles,
                                                                                      existingFeatureToggles.Concat(new[]
                                                                                      {
                                                                                          FeatureToggle.AsynchronousAzureZipDeployFeatureToggle.ToString()
                                                                                      }));
                                                     })
                                        .Execute();

                // Assert
                await DoWithRetries(2,
                                    async () =>
                                    {
                                        await AssertContent(WebSiteResource.Data.DefaultHostName,
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
                
                var settings = BuildAppSettingsJson(new[]
                {
                    ("WEBSITES_CONTAINER_START_TIME_LIMIT", "460", false),
                    ("WEBSITE_SCM_ALWAYS_ON_ENABLED", "true", false)
                });
                
                context.Variables[SpecialVariables.Action.Azure.AppSettings] =  settings.json;
            }
        }
    }
}