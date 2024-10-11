using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.Azure.AppServices;
using Calamari.CloudAccounts;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using HttpClient = System.Net.Http.HttpClient;
using KnownVariables = Calamari.Common.Plumbing.Variables.KnownVariables;

namespace Calamari.AzureWebApp.Tests
{
    [TestFixture]
    public class DeployAzureWebCommandFixture
    {
        string clientId;
        string clientSecret;
        string tenantId;
        string subscriptionId;

        readonly HttpClient client = new HttpClient();

        ArmClient armClient;
        ResourceGroupResource resourceGroupResource;
        WebSiteResource webSiteResource;

        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri = Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri = Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            var resourceGroupName = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}";

            clientId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId, cancellationToken);
            clientSecret = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword, cancellationToken);
            tenantId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId, cancellationToken);
            subscriptionId = await ExternalVariables.Get(ExternalVariable.AzureSubscriptionId, cancellationToken);
            var resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? RandomAzureRegion.GetRandomRegionWithExclusions();

            var servicePrincipalAccount = new AzureServicePrincipalAccount(subscriptionId,
                                                                           clientId,
                                                                           tenantId,
                                                                           clientSecret,
                                                                           "AzureGlobalCloud",
                                                                           resourceManagementEndpointBaseUri,
                                                                           activeDirectoryEndpointBaseUri);

            armClient = servicePrincipalAccount.CreateArmClient(retryOptions =>
                                                                {
                                                                    retryOptions.MaxRetries = 5;
                                                                    retryOptions.Mode = RetryMode.Exponential;
                                                                    retryOptions.Delay = TimeSpan.FromSeconds(2);
                                                                    // AzureAppServiceDeployContainerBehaviorFixture.AzureLinuxContainerSlotDeploy occasional timeout at default 100 seconds
                                                                    retryOptions.NetworkTimeout = TimeSpan.FromSeconds(200);
                                                                });

            //create the resource group
            var subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));

            var response = await subscriptionResource
                                 .GetResourceGroups()
                                 .CreateOrUpdateAsync(WaitUntil.Completed,
                                                      resourceGroupName,
                                                      new ResourceGroupData(new AzureLocation(resourceGroupLocation))
                                                      {
                                                          Tags =
                                                          {
                                                              // give them an expiry of 14 days so if the tests fail to clean them up
                                                              // they will be automatically cleaned up by the Sandbox cleanup process
                                                              // We keep them for 14 days just in case we need to do debugging/investigation
                                                              ["LifetimeInDays"] = "14"
                                                          }
                                                      },
                                                      cancellationToken);

            resourceGroupResource = response.Value;

            var appServicePlanData = new AppServicePlanData(resourceGroupResource.Data.Location)
            {
                Sku = new AppServiceSkuDescription
                {
                    Name = "P1V3",
                    Tier = "PremiumV3"
                }
            };

            var servicePlanResponse = await resourceGroupResource.GetAppServicePlans()
                                                                 .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                                      resourceGroupResource.Data.Name,
                                                                                      appServicePlanData,
                                                                                      cancellationToken);

            var webSiteData = new WebSiteData(resourceGroupResource.Data.Location)
            {
                AppServicePlanId = servicePlanResponse.Value.Id
            };

            var webSiteResponse = await resourceGroupResource.GetWebSites()
                                                             .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                                  resourceGroupResource.Data.Name,
                                                                                  webSiteData,
                                                                                  cancellationToken);

            webSiteResource = webSiteResponse.Value;
        }

        [OneTimeTearDown]
        public virtual async Task Cleanup()
        {
            await resourceGroupResource.DeleteAsync(WaitUntil.Started, cancellationToken: cancellationToken);
        }

        [Test]
        public async Task Deploy_WebApp_Simple()
        {
            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);

                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .Execute();

            await AssertContent(webSiteResource.Data.DefaultHostName, actualText);
        }

        [Test]
        public async Task Deploy_WebApp_Using_AppOffline()
        {
            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "I'm broken";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), "Hello World");
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "App_Offline.htm"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);

                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .Execute();

            var packagePath = TestEnvironment.GetTestPath("Packages", "BrokenApp");

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.AppOffline, bool.TrueString);

                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();

            var response = await client.GetAsync($"https://{webSiteResource.Data.DefaultHostName}", cancellationToken);
            response.IsSuccessStatusCode.Should().BeFalse();
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            var result = await response.Content.ReadAsStringAsync();
            result.Should().Be(actualText);
        }

        [Test]
        public async Task Deploy_WebApp_Using_Checksum()
        {
            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .Execute();

            // We write the file again with same content
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.UseChecksum, bool.TrueString);
                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .WithAssert(result =>
                                                {
                                                    result.FullLog.Should().Contain("Successfully deployed to Azure. 0 objects added. 0 objects updated. 0 objects deleted.");
                                                })
                                    .Execute();

            await AssertContent(webSiteResource.Data.DefaultHostName, actualText);
        }

        [Test]
        public async Task Deploy_WebApp_Preserve_App_Data()
        {
            using var tempPath = TemporaryDirectory.Create();

            Directory.CreateDirectory(Path.Combine(tempPath.DirectoryPath, "App_Data"));
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "App_Data", "newfile1.txt"), "Hello World");
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), "Hello World");

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.RemoveAdditionalFiles, bool.TrueString);
                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .Execute();

            var packagePath = TestEnvironment.GetTestPath("Packages", "AppDataList");

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.RemoveAdditionalFiles, bool.TrueString);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.PreserveAppData, bool.TrueString);
                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();

            await AssertContent(webSiteResource.Data.DefaultHostName, "newfile1.txt\r\nnewfile2.txt\r\n");
        }

        [Test]
        public async Task Deploy_WebApp_Preserve_Files()
        {
            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "Hello World";

            Directory.CreateDirectory(Path.Combine(tempPath.DirectoryPath, "Keep"));
            Directory.CreateDirectory(Path.Combine(tempPath.DirectoryPath, "NotKeep"));
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "Keep", "index.html"), actualText);
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "NotKeep", "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);

                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .Execute();

            using var tempPath2 = TemporaryDirectory.Create();

            File.WriteAllText(Path.Combine(tempPath2.DirectoryPath, "newfile.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.RemoveAdditionalFiles, bool.TrueString);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.PreservePaths, @"\\Keep;\\Keep\\index.html");

                                                     context.WithFilesToCopy(tempPath2.DirectoryPath);
                                                 })
                                    .Execute();

            await AssertContent(webSiteResource.Data.DefaultHostName, actualText, "Keep");
            await AssertContent(webSiteResource.Data.DefaultHostName, actualText, "newfile.html");

            var response = await client.GetAsync($"https://{webSiteResource.Data.DefaultHostName}/NotKeep", cancellationToken);
            response.IsSuccessStatusCode.Should().BeFalse();
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task Deploy_WebApp_To_A_Slot()
        {
            var slotName = "staging";

            var slotResponse = await webSiteResource.GetWebSiteSlots()
                                                    .CreateOrUpdateAsync(WaitUntil.Completed, slotName, webSiteResource.Data, cancellationToken);

            var slotResource = slotResponse.Value;

            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.WebAppSlot, slotName);

                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .Execute();

            await AssertContent(slotResource.Data.DefaultHostName, actualText);
        }

        [Test]
        public async Task Deploy_WebApp_From_Package()
        {
            using var tempPath = TemporaryDirectory.Create();
            var actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.WithNewNugetPackage(tempPath.DirectoryPath, "Hello", "1.0.0");
                                                 })
                                    .Execute();

            await AssertContent(webSiteResource.Data.DefaultHostName, actualText);
        }

        [Test]
        public async Task Deploy_WebApp_With_PhysicalPath()
        {
            using var tempPath = TemporaryDirectory.Create();
            var actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);
            const string rootPath = "Hello";

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.PhysicalPath, rootPath);

                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .Execute();

            await AssertContent(webSiteResource.Data.DefaultHostName, actualText, rootPath);
        }

        [Test]
        [RequiresPowerShell5OrAbove]
        public async Task Deploy_WebApp_Ensure_Tools_Are_Configured()
        {
            using var tempPath = TemporaryDirectory.Create();
            const string actualText = "Hello World";

            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "index.html"), actualText);
            var psScript = @"
$ErrorActionPreference = 'Continue'
az --version
az group list";
            File.WriteAllText(Path.Combine(tempPath.DirectoryPath, "PreDeploy.ps1"), psScript);

            // This should be references from Sashimi.Server.Contracts, since Calamari.AzureWebApp is a net461 project this cannot be included.
            var AccountType = "Octopus.Account.AccountType";

            await CommandTestBuilder.CreateAsync<DeployAzureWebCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     context.Variables.Add(AccountType, "AzureServicePrincipal");
                                                     AddDefaults(context);
                                                     context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);
                                                     context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.Deploy, ScriptSyntax.PowerShell), psScript);
                                                     context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PreDeploy, ScriptSyntax.CSharp), "Console.WriteLine(\"Hello from C#\");");
                                                     context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PostDeploy, ScriptSyntax.FSharp), "printfn \"Hello from F#\"");
                                                     context.WithFilesToCopy(tempPath.DirectoryPath);
                                                 })
                                    .WithAssert(result =>
                                                {
                                                    result.FullLog.Should().Contain("Hello from C#");
                                                    result.FullLog.Should().Contain("Hello from F#");
                                                })
                                    .Execute();

            await AssertContent(webSiteResource.Data.DefaultHostName, actualText);
        }

        async Task AssertContent(string hostName, string actualText, string rootPath = null)
        {
            var response = await RetryPolicies.TestsTransientHttpErrorsPolicy.ExecuteAsync(async context =>
                                                                                           {
                                                                                               var r = await client.GetAsync($"https://{hostName}/{rootPath}", cancellationToken);
                                                                                               if (!r.IsSuccessStatusCode)
                                                                                               {
                                                                                                   var messageContent = await r.Content.ReadAsStringAsync();
                                                                                                   TestContext.WriteLine($"Unable to retrieve content from https://{hostName}/{rootPath}, failed with: {messageContent}");
                                                                                               }

                                                                                               r.EnsureSuccessStatusCode();
                                                                                               return r;
                                                                                           },
                                                                                           contextData: new Dictionary<string, object>());

            var result = await response.Content.ReadAsStringAsync();
            result.Should().Contain(actualText);
        }

        void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add(AzureAccountVariables.SubscriptionId, subscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId, tenantId);
            context.Variables.Add(AzureAccountVariables.ClientId, clientId);
            context.Variables.Add(AzureAccountVariables.Password, clientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, webSiteResource.Data.Name);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroupResource.Data.Name);
        }
    }
}