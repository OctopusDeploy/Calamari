using Calamari.AzureAppService.Behaviors;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    public class TargetDiscoveryBehaviourIntegrationTestFixture : AppServiceIntegrationTestWithStaticResources
    {
        // https://portal.azure.com/#@octopusdeploy.onmicrosoft.com/resource/subscriptions/cf21dc34-73dc-4d7d-bd86-041884e0bc75/resourcegroups/calamari-testing-static-rg/providers/Microsoft.Web/sites/calamari-testing-static-target-discovery/appServices
        const string WebAppName = "calamari-testing-static-target-discovery";
        readonly List<string> slotNames = ["blue", "green"];

        const string Type = "Azure";
        const string AuthenticationMethod = "ServicePrincipal";
        const string AccountId = "Accounts-1";
        const string TenantedDeploymentModeName = "TenantedOrUntenanted";

        const string EnvironmentTagValue = "static-testing-env";
        const string WebAppRoleTagValue = "static-testing-web-app-role";
        const string WebAppSlotRoleTagValue = "static-testing-web-app-slot-role";

        [Test]
        public async Task Execute_WebAppWithMatchingTags_CreatesCorrectTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            CreateVariables(context, WebAppRoleTagValue);

            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Act
            await sut.Execute(context);

            // Assert
            var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                WebAppName,
                AccountId,
                WebAppRoleTagValue,
                null,
                null,
                TenantedDeploymentModeName);
            var serviceMessageString = serviceMessageToCreateWebAppTarget.ToString();
            log.StandardOut.Should().Contain(serviceMessageString);
        }

        [Test]
        public async Task Execute_WebAppWithNonMatchingTags_CreatesNoTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);

            const string role = "a-different-role";

            CreateVariables(context, role);

            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Act
            await sut.Execute(context);

            // Assert
            var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                WebAppName,
                AccountId,
                role,
                null,
                null,
                TenantedDeploymentModeName);

            log.StandardOut.Should().NotContain(serviceMessageToCreateWebAppTarget.ToString(), "The web app target should not be created as the role tag did not match");
        }

        [Test]
        public async Task Execute_MultipleWebAppSlotsWithTags_WebAppHasNoTags_CreatesCorrectTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);

            CreateVariables(context, null, WebAppSlotRoleTagValue);

            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Act
            await sut.Execute(context);

            var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                WebAppName,
                AccountId,
                WebAppRoleTagValue,
                null,
                null,
                null);
            log.StandardOut.Should().NotContain(serviceMessageToCreateWebAppTarget.ToString(), "A target should not be created for the web app itself, only for slots within the web app");

            // Assert
            foreach (var slotName in slotNames)
            {
                var serviceMessageToCreateTargetForSlot = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                    WebAppName,
                    AccountId,
                    WebAppSlotRoleTagValue,
                    null,
                    slotName,
                    null);
                log.StandardOut.Should().Contain(serviceMessageToCreateTargetForSlot.ToString());
            }
        }

        [Test]
        public async Task Execute_MultipleWebAppSlotsWithTags_WebAppWithTags_CreatesCorrectTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);
            CreateVariables(context, WebAppRoleTagValue, WebAppSlotRoleTagValue);
            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            // Act
            await sut.Execute(context);

            // Assert
            var serviceMessageToCreateWebAppTarget = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                WebAppName,
                AccountId,
                WebAppRoleTagValue,
                null,
                null,
                TenantedDeploymentModeName);
            log.StandardOut.Should().Contain(serviceMessageToCreateWebAppTarget.ToString(), "A target should be created for the web app itself as well as for the slots");

            foreach (var slotName in slotNames)
            {
                var serviceMessageToCreateTargetForSlot = TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                    WebAppName,
                    AccountId,
                    WebAppSlotRoleTagValue,
                    null,
                    slotName,
                    null);
                log.StandardOut.Should().Contain(serviceMessageToCreateTargetForSlot.ToString());
            }
        }

        [Test]
        public async Task Execute_MultipleWebAppSlotsWithPartialTags_WebAppWithPartialTags_CreatesNoTargets()
        {
            // Arrange
            var variables = new CalamariVariables();
            var context = new RunningDeployment(variables);

            CreateVariables(context, null, WebAppSlotRoleTagValue);

            var log = new InMemoryLog();
            var sut = new TargetDiscoveryBehaviour(log);

            const string partialMatchSlotName = "partial-match";

            // Act
            await sut.Execute(context);

            // Assert
            var serviceMessageToCreateWebAppTarget =
                TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                    WebAppName,
                    AccountId,
                    WebAppRoleTagValue,
                    null,
                    null,
                    TenantedDeploymentModeName);
            log.StandardOut.Should()
               .NotContain(serviceMessageToCreateWebAppTarget.ToString(),
                   "A target should not be created for the web app as the tags directly on the web app do not match, even though when combined with the slot tags they do");

            var serviceMessageToCreateTargetForSlot =
                TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(ResourceGroupName,
                    WebAppName,
                    AccountId,
                    WebAppSlotRoleTagValue,
                    null,
                    partialMatchSlotName,
                    null);
            log.StandardOut.Should()
               .NotContain(serviceMessageToCreateTargetForSlot.ToString(),
                   "A target should not be created for the web app slot as the tags directly on the slot do not match, even though when combined with the web app tags they do");
        }

        void CreateVariables(RunningDeployment context,
                             string webAppRoleTagValue = null,
                             string slotRoleTagValue = null)
        {
            var tagValues = string.Join(',', new[] { webAppRoleTagValue, slotRoleTagValue }.WhereNotNull().Select(v => $"\"{v}\""));

            var targetDiscoveryContext = $$"""
                                           {
                                               "scope": {
                                                   "spaceName": "default",
                                                   "environmentName": "{{EnvironmentTagValue}}",
                                                   "projectName": "my-test-project",
                                                   "tenantName": null,
                                                   "roles": [{{tagValues}}]
                                               },
                                               "authentication": {
                                                   "type": "{{Type}}",
                                                   "accountId": "{{AccountId}}",
                                                   "authenticationMethod": "{{AuthenticationMethod}}",
                                                   "accountDetails": {
                                                       "subscriptionNumber": "{{SubscriptionId}}",
                                                       "clientId": "{{ClientId}}",
                                                       "tenantId": "{{TenantId}}",
                                                       "password": "{{ClientSecret}}",
                                                       "azureEnvironment": "",
                                                       "resourceManagementEndpointBaseUri": "",
                                                       "activeDirectoryEndpointBaseUri": ""
                                                   }
                                               }
                                           }
                                           """;

            context.Variables.Add("Octopus.TargetDiscovery.Context", targetDiscoveryContext);
        }
    }
}