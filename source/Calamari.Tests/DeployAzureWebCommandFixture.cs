﻿using System.IO;
using System.Net;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Tests.Shared;
using Calamari.Tests.Shared.Helpers;
using FluentAssertions;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using NUnit.Framework;
using HttpClient = System.Net.Http.HttpClient;
using OperatingSystem = Microsoft.Azure.Management.AppService.Fluent.OperatingSystem;

namespace Calamari.AzureWebApp.Tests
{
    [TestFixture]
    public class DeployAzureWebCommandFixture
    {
        IAppServicePlan appServicePlan;
        IResourceGroup resourceGroup;
        IAzure azure;
        string clientId;
        string clientSecret;
        string tenantId;
        string subscriptionId;

        readonly HttpClient client = new HttpClient();

        [OneTimeSetUp]
        public async Task Setup()
        {
            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);
            var resourceGroupName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId, clientSecret, tenantId,
                AzureEnvironment.AzureGlobalCloud);

            azure = Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials)
                .WithSubscription(subscriptionId);

            resourceGroup = await azure.ResourceGroups
                .Define(resourceGroupName)
                .WithRegion(Region.USWest)
                .CreateAsync();

            appServicePlan = await azure.AppServices.AppServicePlans
                .Define(SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60))
                .WithRegion(resourceGroup.Region)
                .WithExistingResourceGroup(resourceGroup)
                .WithPricingTier(PricingTier.StandardS1)
                .WithOperatingSystem(OperatingSystem.Windows)
                .CreateAsync();
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            if (resourceGroup != null)
            {
                await azure.ResourceGroups.DeleteByNameAsync(resourceGroup.Name);
            }
        }

        [Test]
        public async Task Deploy_WebApp_Simple()
        {
            var webAppName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);

            var webApp = await CreateWebApp(webAppName);

            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);

                    context.WithFilesToCopy(tempPath.DirectoryPath);
                })
                .Execute();

            await AssertContent(webApp.DefaultHostName, actualText);
        }

        [Test]
        public async Task Deploy_WebApp_Using_AppOffline()
        {
            var webAppName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);

            var webApp = await CreateWebApp(webAppName);

            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "I'm broken";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), "Hello World");
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "App_Offline.htm"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);

                    context.WithFilesToCopy(tempPath.DirectoryPath);
                })
                .Execute();

            var packagePath = TestEnvironment.GetTestPath("Packages", "BrokenApp");

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);
                    context.Variables.Add(SpecialVariables.Action.Azure.AppOffline, bool.TrueString);

                    context.WithFilesToCopy(packagePath);
                })
                .Execute();

            var response = await client.GetAsync($"https://{webApp.DefaultHostName}");
            response.IsSuccessStatusCode.Should().BeFalse();
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            var result = await response.Content.ReadAsStringAsync();
            result.Should().Be(actualText);
        }

        [Test]
        public async Task Deploy_WebApp_Using_Checksum()
        {
            var webAppName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);

            var webApp = await CreateWebApp(webAppName);

            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);
                    context.WithFilesToCopy(tempPath.DirectoryPath);
                })
                .Execute();

            // We write the file again with same content
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);
                    context.Variables.Add(SpecialVariables.Action.Azure.UseChecksum, bool.TrueString);
                    context.WithFilesToCopy(tempPath.DirectoryPath);
                })
                .WithAssert(result =>
                {
                    result.FullLog.Should().Contain("Successfully deployed to Azure. 0 objects added. 0 objects updated. 0 objects deleted.");
                })
                .Execute();

            await AssertContent(webApp.DefaultHostName, actualText);
        }

        [Test]
        public async Task Deploy_WebApp_Preserve_App_Data()
        {
            var webAppName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);

            var webApp = await CreateWebApp(webAppName);

            using var tempPath = TemporaryDirectory.Create();

            Directory.CreateDirectory(Path.Combine(tempPath.DirectoryPath, "App_Data"));
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "App_Data", "newfile1.txt"), "Hello World");
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), "Hello World");

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);
                    context.Variables.Add(SpecialVariables.Action.Azure.RemoveAdditionalFiles, bool.TrueString);
                    context.WithFilesToCopy(tempPath.DirectoryPath);
                })
                .Execute();

            var packagePath = TestEnvironment.GetTestPath("Packages", "AppDataList");

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);
                    context.Variables.Add(SpecialVariables.Action.Azure.RemoveAdditionalFiles, bool.TrueString);
                    context.Variables.Add(SpecialVariables.Action.Azure.PreserveAppData, bool.TrueString);
                    context.WithFilesToCopy(packagePath);
                })
                .Execute();

            await AssertContent(webApp.DefaultHostName, "newfile1.txt\r\nnewfile2.txt\r\n");
        }

        [Test]
        public async Task Deploy_WebApp_Preserve_Files()
        {
            var webAppName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);

            var webApp = await CreateWebApp(webAppName);

            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "Hello World";

            Directory.CreateDirectory(Path.Combine(tempPath.DirectoryPath, "Keep"));
            Directory.CreateDirectory(Path.Combine(tempPath.DirectoryPath, "NotKeep"));
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "Keep", "index.html"), actualText);
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "NotKeep", "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);

                    context.WithFilesToCopy(tempPath.DirectoryPath);
                })
                .Execute();

            using var tempPath2 = TemporaryDirectory.Create();

            File.WriteAllText(Path.Combine(tempPath2.DirectoryPath, "newfile.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);
                    context.Variables.Add(SpecialVariables.Action.Azure.RemoveAdditionalFiles, bool.TrueString);
                    context.Variables.Add(SpecialVariables.Action.Azure.PreservePaths, "\\\\Keep;\\\\Keep\\\\index.html");

                    context.WithFilesToCopy(tempPath2.DirectoryPath);
                })
                .Execute();

            await AssertContent(webApp.DefaultHostName, actualText, "Keep");
            await AssertContent(webApp.DefaultHostName, actualText, "newfile.html");

            var response = await client.GetAsync($"https://{webApp.DefaultHostName}/NotKeep");
            response.IsSuccessStatusCode.Should().BeFalse();
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task Deploy_WebApp_To_A_Slot()
        {
            var webAppName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);
            var slotName = "staging";

            var webApp = await CreateWebApp(webAppName);
            var deploymentSlot = await webApp.DeploymentSlots.Define(slotName)
                .WithConfigurationFromParent()
                .WithAutoSwapSlotName("production")
                .CreateAsync();

            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);
                    context.Variables.Add(SpecialVariables.Action.Azure.WebAppSlot, slotName);

                    context.WithFilesToCopy(tempPath.DirectoryPath);
                })
                .Execute();

            await AssertContent(deploymentSlot.DefaultHostName, actualText);
        }

        [Test]
        public async Task Deploy_WebApp_From_Package()
        {
            var webAppName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);

            var webApp = await CreateWebApp(webAppName);

            using var tempPath = TemporaryDirectory.Create();
            var actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);
                    context.WithNewNugetPackage(tempPath.DirectoryPath, "Hello", "1.0.0");
                })
                .Execute();

            await AssertContent(webApp.DefaultHostName, actualText);
        }

        [Test]
        public async Task Deploy_WebApp_With_PhysicalPath()
        {
            var webAppName = SdkContext.RandomResourceName(nameof(DeployAzureWebCommandFixture), 60);

            var webApp = await CreateWebApp(webAppName);

            using var tempPath = TemporaryDirectory.Create();
            var actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);
            const string rootPath = "Hello";

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                .WithArrange(context =>
                {
                    AddDefaults(context, webAppName);
                    context.Variables.Add(SpecialVariables.Action.Azure.PhysicalPath, rootPath);

                    context.WithFilesToCopy(tempPath.DirectoryPath);
                })
                .Execute();

            await AssertContent(webApp.DefaultHostName, actualText, rootPath);
        }

        Task<IWebApp> CreateWebApp(string webAppName)
        {
            return azure.WebApps
                .Define(webAppName)
                .WithExistingWindowsPlan(appServicePlan)
                .WithExistingResourceGroup(resourceGroup)
                .WithRuntimeStack(WebAppRuntimeStack.NETCore)
                .CreateAsync();
        }

        async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var result= await client.GetStringAsync($"https://{hostName}/{rootPath}");

            result.Should().Be(actualText);
        }

        void AddDefaults(CommandTestBuilderContext context, string webAppName)
        {
            context.Variables.Add(AzureAccountVariables.SubscriptionId,
                subscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId,
                tenantId);
            context.Variables.Add(AzureAccountVariables.ClientId,
                clientId);
            context.Variables.Add(AzureAccountVariables.Password,
                clientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, webAppName);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroup.Name);
        }
    }
}