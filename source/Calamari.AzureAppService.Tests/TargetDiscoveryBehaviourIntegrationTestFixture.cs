using Azure.ResourceManager.Resources;
using Calamari.AzureAppService.Behaviors;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Calamari.Common.Plumbing.Extensions;
using Polly.Retry;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class TargetDiscoveryBehaviourIntegrationTestFixture : AppServiceIntegrationTest
    {
        private readonly string appName = Guid.NewGuid().ToString();
        private readonly List<string> slotNames = new List<string> { "blue", "green" };
        private static readonly string Type = "Azure";
        private static readonly string AuthenticationMethod = "ServicePrincipal";
        private static readonly string AccountId = "Accounts-1";
        private static readonly string Role = "my-azure-app-role";
        private static readonly string EnvironmentName = "dev";
        static readonly string TenantedDeploymentModeName = "TenantedOrUntenanted";
        private RetryPolicy retryPolicy;

        private AppServicePlanResource appServicePlanResource;

        protected override async Task ConfigureTestResources(ResourceGroupResource resourceGroup)
        {
            var response = await resourceGroup.GetAppServicePlans()
                                              .CreateOrUpdateAsync(WaitUntil.Completed,
                                                                   ResourceGroupName,
                                                                   new AppServicePlanData(resourceGroup.Data.Location)
                                                                   {
                                                                       Sku = new AppServiceSkuDescription
                                                                       {
                                                                           Name = "P1V3",
                                                                           Tier = "PremiumV3"
                                                                       }
                                                                   });

            appServicePlanResource = response.Value;
        }

        [SetUp]
        public async Task CreateOrResetWebAppAndSlots()
        {
            // Call update on the web app and each slot without and tags
            // to reset it for each test.
            WebSiteResource = await CreateOrUpdateTestWebApp();
            await CreateOrUpdateTestWebAppSlots(WebSiteResource);
        }

        [Test]
        public async Task Execute_WebAppWithMatchingTags_CreatesCorrectTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            this.CreateVariables(context);
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Set expected tags on our web app
            var tags = new Dictionary<string, string>
            {
                { TargetTags.EnvironmentTagName, EnvironmentName },
                { TargetTags.RoleTagName, Role },
                { TargetTags.TenantedDeploymentModeTagName, TenantedDeploymentModeName}
            };

            await CreateOrUpdateTestWebApp(tags);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(15));

            await Eventually.ShouldEventually(async () =>
                                              {
                                                  // Act
                                                  await sut.Execute(context);

                                                  // Assert
                                                  var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                                                                                                                                                           appName,
                                                                                                                                                           AccountId,
                                                                                                                                                           Role,
                                                                                                                                                           null,
                                                                                                                                                           null,
                                                                                                                                                           TenantedDeploymentModeName);
                                                  var serviceMessageString = serviceMessageToCreateWebAppTarget.ToString();
                                                  log.StandardOut.Should().Contain(serviceMessageString);
                                              },
                                              log,
                                              cancellationTokenSource.Token);
        }

        [Test]
        public async Task Execute_WebAppWithNonMatchingTags_CreatesNoTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            this.CreateVariables(context);
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Set expected tags on our web app
            var tags = new Dictionary<string, string>
            {
                { TargetTags.EnvironmentTagName, EnvironmentName },
                { TargetTags.RoleTagName, Role },
            };

            await CreateOrUpdateTestWebApp(tags);

            // Act
            await sut.Execute(context);

            // Assert
            var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                                                                                                                     appName,
                                                                                                                     AccountId,
                                                                                                                     "a-different-role",
                                                                                                                     null,
                                                                                                                     null,
                                                                                                                     null);
            log.StandardOut.Should().NotContain(serviceMessageToCreateWebAppTarget.ToString(), "The web app target should not be created as the role tag did not match");
        }

        [Test]
        public async Task Execute_MultipleWebAppSlotsWithTags_WebAppHasNoTags_CreatesCorrectTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            CreateVariables(context);
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Set expected tags on each slot of the web app but not the web app itself
            var tags = new Dictionary<string, string>
            {
                { TargetTags.EnvironmentTagName, EnvironmentName },
                { TargetTags.RoleTagName, Role },
            };

            await CreateOrUpdateTestWebAppSlots(WebSiteResource, tags);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(15));

            await Eventually.ShouldEventually(async () =>
                                              {
                                                  // Act
                                                  await sut.Execute(context);

                                                  var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                                                                                                                                                           appName,
                                                                                                                                                           AccountId,
                                                                                                                                                           Role,
                                                                                                                                                           null,
                                                                                                                                                           null,
                                                                                                                                                           null);
                                                  log.StandardOut.Should().NotContain(serviceMessageToCreateWebAppTarget.ToString(), "A target should not be created for the web app itself, only for slots within the web app");

                                                  // Assert
                                                  foreach (var slotName in slotNames)
                                                  {
                                                      var serviceMessageToCreateTargetForSlot = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                                                                                                                                                                appName,
                                                                                                                                                                AccountId,
                                                                                                                                                                Role,
                                                                                                                                                                null,
                                                                                                                                                                slotName,
                                                                                                                                                                null);
                                                      log.StandardOut.Should().Contain(serviceMessageToCreateTargetForSlot.ToString());
                                                  }
                                              },
                                              log,
                                              cancellationTokenSource.Token);
        }

        [Test]
        public async Task Execute_MultipleWebAppSlotsWithTags_WebAppWithTags_CreatesCorrectTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            CreateVariables(context);
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Set expected tags on each slot of the web app AND the web app itself
            var tags = new Dictionary<string, string>
            {
                { TargetTags.EnvironmentTagName, EnvironmentName },
                { TargetTags.RoleTagName, Role },
            };

            var webSiteResource =await CreateOrUpdateTestWebApp(tags);
            await CreateOrUpdateTestWebAppSlots(webSiteResource,tags);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(15));

            await Eventually.ShouldEventually(async () =>
                                              {
                                                  // Act
                                                  await sut.Execute(context);

                                                  var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                                                                                                                                                           appName,
                                                                                                                                                           AccountId,
                                                                                                                                                           Role,
                                                                                                                                                           null,
                                                                                                                                                           null,
                                                                                                                                                           null);
                                                  log.StandardOut.Should().Contain(serviceMessageToCreateWebAppTarget.ToString(), "A target should be created for the web app itself as well as for the slots");

                                                  // Assert
                                                  foreach (var slotName in slotNames)
                                                  {
                                                      var serviceMessageToCreateTargetForSlot = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                                                                                                                                                                appName,
                                                                                                                                                                AccountId,
                                                                                                                                                                Role,
                                                                                                                                                                null,
                                                                                                                                                                slotName,
                                                                                                                                                                null);
                                                      log.StandardOut.Should().Contain(serviceMessageToCreateTargetForSlot.ToString());
                                                  }
                                              },
                                              log,
                                              cancellationTokenSource.Token);
        }

        [Test]
        public async Task Execute_MultipleWebAppSlotsWithPartialTags_WebAppWithPartialTags_CreatesNoTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            CreateVariables(context);
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Set partial tags on each slot of the web app AND the remaining ones on the web app itself
            var webAppTags = new Dictionary<string, string>
            {
                { TargetTags.EnvironmentTagName, EnvironmentName },
            };

            var slotTags = new Dictionary<string, string>
            {
                { TargetTags.RoleTagName, Role },
            };

            var webSiteResource = await CreateOrUpdateTestWebApp(webAppTags);
            await CreateOrUpdateTestWebAppSlots(webSiteResource, slotTags);
            var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(15));

            await Eventually.ShouldEventually(async () =>
                                              {
                                                  // Act
                                                  await sut.Execute(context);

                                                  var serviceMessageToCreateWebAppTarget =
                                                      TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                                                                                                                      appName,
                                                                                                                      AccountId,
                                                                                                                      Role,
                                                                                                                      null,
                                                                                                                      null,
                                                                                                                      null);
                                                  log.StandardOut.Should()
                                                     .NotContain(serviceMessageToCreateWebAppTarget.ToString(),
                                                                 "A target should not be created for the web app as the tags directly on the web app do not match, even though when combined with the slot tags they do");

                                                  // Assert
                                                  foreach (var slotName in slotNames)
                                                  {
                                                      var serviceMessageToCreateTargetForSlot =
                                                          TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                                                                                                                          appName,
                                                                                                                          AccountId,
                                                                                                                          Role,
                                                                                                                          null,
                                                                                                                          slotName,
                                                                                                                          null);
                                                      log.StandardOut.Should()
                                                         .NotContain(serviceMessageToCreateTargetForSlot.ToString(),
                                                                     "A target should not be created for the web app slot as the tags directly on the slot do not match, even though when combined with the web app tags they do");
                                                  }
                                              },
                                              log,
                                              cancellationTokenSource.Token);
        }

        private async Task<WebSiteResource> CreateOrUpdateTestWebApp(IDictionary<string, string> tags = null)
        {
            var data = new WebSiteData(ResourceGroupResource.Data.Location)
            {
                AppServicePlanId = appServicePlanResource.Id
            };

            if (tags != null)
                data.Tags.AddRange(tags);

            var response = await ResourceGroupResource.GetWebSites()
                                       .CreateOrUpdateAsync(WaitUntil.Completed,
                                                            appName,
                                                            data);

            return response.Value;
        }

        private async Task CreateOrUpdateTestWebAppSlots(WebSiteResource webSiteResource, Dictionary<string, string> tags = null)
        {
            var webSiteData = webSiteResource.Data;

            if (tags != null)
                webSiteData.Tags.AddRange(tags);

            var slotTasks = new List<Task>();
            
            foreach (var slotName in slotNames)
            {
                var task = webSiteResource.GetWebSiteSlots()
                               .CreateOrUpdateAsync(WaitUntil.Completed,
                                                    slotName,
                                                    webSiteData
                                                   );
                slotTasks.Add(task);
            }

            await Task.WhenAll(slotTasks);
        }

        private void CreateVariables(RunningDeployment context)
        {
            string targetDiscoveryContext = $@"{{
    ""scope"": {{
        ""spaceName"": ""default"",
        ""environmentName"": ""{EnvironmentName}"",
        ""projectName"": ""my-test-project"",
        ""tenantName"": null,
        ""roles"": [""{Role}""]
    }},
    ""authentication"": {{
        ""type"": ""{Type}"",
        ""accountId"": ""{AccountId}"",
        ""authenticationMethod"": ""{AuthenticationMethod}"",
        ""accountDetails"": {{
            ""subscriptionNumber"": ""{SubscriptionId}"",
            ""clientId"": ""{ClientId}"",
            ""tenantId"": ""{TenantId}"",
            ""password"": ""{ClientSecret}"",
            ""azureEnvironment"": """",
            ""resourceManagementEndpointBaseUri"": """",
            ""activeDirectoryEndpointBaseUri"": """"
        }}
    }}
}}
";

            context.Variables.Add("Octopus.TargetDiscovery.Context", targetDiscoveryContext);
        }
    }
}