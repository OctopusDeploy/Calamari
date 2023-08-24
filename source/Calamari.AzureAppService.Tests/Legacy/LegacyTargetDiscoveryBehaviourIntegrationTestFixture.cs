using Azure.Identity;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Calamari.AzureAppService.Behaviors;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Microsoft.Azure.Management.WebSites;
using Microsoft.Azure.Management.WebSites.Models;
using Microsoft.Rest;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Polly.Retry;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class LegacyTargetDiscoveryBehaviourIntegrationTestFixture
    {
        private string clientId;
        private string clientSecret;
        private string tenantId;
        private string subscriptionId;
        private string resourceGroupName;
        private string authToken;
        private ResourceGroupsOperations resourceGroupClient;
        private WebSiteManagementClient webMgmtClient;
        private ResourceGroup resourceGroup;
        private AppServicePlan svcPlan;
        private string appName = Guid.NewGuid().ToString();
        private List<string> slotNames = new List<string> { "blue", "green" };
        private static readonly string Type = "Azure";
        private static readonly string AccountId = "Accounts-1";
        private static readonly string Role = "my-azure-app-role";
        private static readonly string EnvironmentName = "dev";
        private RetryPolicy retryPolicy;

        [OneTimeSetUp]
        public async Task Setup()
        {
            retryPolicy = RetryPolicyFactory.CreateForHttp429();
            
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

            authToken = await Auth.GetAuthTokenAsync(tenantId, clientId, clientSecret, resourceManagementEndpointBaseUri, activeDirectoryEndpointBaseUri);

            var resourcesClient = new ResourcesManagementClient(subscriptionId,
                new ClientSecretCredential(tenantId, clientId, clientSecret));

            resourceGroupClient = resourcesClient.ResourceGroups;

            resourceGroup = new ResourceGroup(resourceGroupLocation);
            resourceGroup = await resourceGroupClient.CreateOrUpdateAsync(resourceGroupName, resourceGroup);

            webMgmtClient = new WebSiteManagementClient(new TokenCredentials(authToken))
            {
                SubscriptionId = subscriptionId,
                HttpClient = { BaseAddress = new Uri(DefaultVariables.ResourceManagementEndpoint), Timeout = TimeSpan.FromMinutes(5) },
            };

            svcPlan = await retryPolicy.ExecuteAsync(async () => await webMgmtClient.AppServicePlans.CreateOrUpdateAsync(resourceGroup.Name,
                                                                                                                         resourceGroup.Name,
                                                                                                                         new AppServicePlan(resourceGroup.Location) { Sku = new SkuDescription("S1", "Standard") }
                                                                                                                        ));
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            if (resourceGroupClient != null)
                await resourceGroupClient.StartDeleteAsync(resourceGroupName);
        }

        [SetUp]
        public async Task CreateOrResetWebAppAndSlots()
        {
            // Call update on the web app and each slot without and tags
            // to reset it for each test.
            await CreateOrUpdateTestWebApp();
            await CreateOrUpdateTestWebAppSlots();
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
            };

            await CreateOrUpdateTestWebApp(tags);

            await Eventually.ShouldEventually(async () =>
            {
                // Act
                await sut.Execute(context);

                // Assert
                var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(resourceGroupName, appName, AccountId, Role, null, null);
                var serviceMessageString = serviceMessageToCreateWebAppTarget.ToString();
                log.StandardOut.Should().Contain(serviceMessageString);
            }, log, CancellationToken.None);
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
            var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(resourceGroupName, appName, AccountId, "a-different-role", null, null);
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

            await CreateOrUpdateTestWebAppSlots(tags);

            await Eventually.ShouldEventually(async () =>
            {
                // Act
                await sut.Execute(context);

                var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(resourceGroupName, appName, AccountId, Role, null, null);
                log.StandardOut.Should().NotContain(serviceMessageToCreateWebAppTarget.ToString(), "A target should not be created for the web app itself, only for slots within the web app");

                // Assert
                foreach (var slotName in slotNames)
                {
                    var serviceMessageToCreateTargetForSlot = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(resourceGroupName, appName, AccountId, Role, null, slotName);
                    log.StandardOut.Should().Contain(serviceMessageToCreateTargetForSlot.ToString());
                }
            }, log, CancellationToken.None);
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

            await CreateOrUpdateTestWebApp(tags);
            await CreateOrUpdateTestWebAppSlots(tags);

            await Eventually.ShouldEventually(async () =>
            {
                // Act
                await sut.Execute(context);

                var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(resourceGroupName, appName, AccountId, Role, null, null);
                log.StandardOut.Should().Contain(serviceMessageToCreateWebAppTarget.ToString(), "A target should be created for the web app itself as well as for the slots");

                // Assert
                foreach (var slotName in slotNames)
                {
                    var serviceMessageToCreateTargetForSlot = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(resourceGroupName, appName, AccountId, Role, null, slotName);
                    log.StandardOut.Should().Contain(serviceMessageToCreateTargetForSlot.ToString());
                }
            }, log, CancellationToken.None);
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

            await CreateOrUpdateTestWebApp(webAppTags);
            await CreateOrUpdateTestWebAppSlots(slotTags);

            await Eventually.ShouldEventually(async () =>
            {
                // Act
                await sut.Execute(context);

                var serviceMessageToCreateWebAppTarget =
                    TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(resourceGroupName, appName,
                        AccountId, Role, null, null);
                log.StandardOut.Should().NotContain(serviceMessageToCreateWebAppTarget.ToString(),
                    "A target should not be created for the web app as the tags directly on the web app do not match, even though when combined with the slot tags they do");

                // Assert
                foreach (var slotName in slotNames)
                {
                    var serviceMessageToCreateTargetForSlot =
                        TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(resourceGroupName, appName,
                            AccountId, Role, null, slotName);
                    log.StandardOut.Should().NotContain(serviceMessageToCreateTargetForSlot.ToString(),
                        "A target should not be created for the web app slot as the tags directly on the slot do not match, even though when combined with the web app tags they do");
                }
            }, log, CancellationToken.None);
        }

        private async Task CreateOrUpdateTestWebApp(Dictionary<string, string> tags = null)
        {
            await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.CreateOrUpdateAsync(
                                                                                                       resourceGroupName,
                                                                                                       appName,
                                                                                                       new Site(resourceGroup.Location, tags: tags) { ServerFarmId = svcPlan.Id }));
        }

        private async Task CreateOrUpdateTestWebAppSlots(Dictionary<string, string> tags = null)
        {
            foreach (var slotName in slotNames)
            {
                await retryPolicy.ExecuteAsync(async () => await webMgmtClient.WebApps.CreateOrUpdateSlotAsync(
                                                                                                               resourceGroup.Name,
                                                                                                               appName,
                                                                                                               new Site(resourceGroup.Location, tags: tags) { ServerFarmId = svcPlan.Id },
                                                                                                               slotName));
            }
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
        ""accountDetails"": {{
            ""subscriptionNumber"": ""{subscriptionId}"",
            ""clientId"": ""{clientId}"",
            ""tenantId"": ""{tenantId}"",
            ""password"": ""{clientSecret}"",
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