using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Behaviors;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Calamari.Contracts.TargetDiscovery;

namespace Calamari.AzureAppService.Tests;

[TestFixture]
public class TargetDiscoveryBehaviourUnitTestFixture
{
    const string WebAppName = "calamari-testing-static-target-discovery";
    const string ResourceGroupName = "calamari-testing-static-rg";
    const string AccountId = "Accounts-1";
    const string TenantedDeploymentModeName = "TenantedOrUntenanted";
    const string EnvironmentTagValue = "static-testing-env";
    const string WebAppRoleTagValue = "static-testing-web-app-role";
    const string WebAppSlotRoleTagValue = "static-testing-web-app-slot-role";
    static readonly string[] SlotNames = { "blue", "green" };

    [Test]
    public async Task Execute_LogsError_WhenContextIsMissing()
    {
        // Arrange
        var variables = new CalamariVariables();
        var context = new RunningDeployment(variables);
        var log = new InMemoryLog();
        var sut = new TargetDiscoveryBehaviour(log, DiscovererReturning());

        // Act
        await sut.Execute(context);

        // Assert
        log.StandardOut.Should().Contain(line => line.Contains("Could not find target discovery context in variable"));
        log.StandardOut.Should().Contain(line => line.Contains("Aborting target discovery."));
    }

    [Test]
    public async Task Exectute_LogsError_WhenContextIsInIncorrectFormat()
    {
        // Arrange
        var variables = new CalamariVariables();
        var context = new RunningDeployment(variables);
        context.Variables.Add("Octopus.TargetDiscovery.Context", @"{ authentication: { authenticationMethod: ""ServicePrincipal""}}");
        var log = new InMemoryLog();
        var sut = new TargetDiscoveryBehaviour(log, DiscovererReturning());

        // Act
        await sut.Execute(context);

        // Assert
        log.StandardOut.Should().Contain(line => line.Contains("Target discovery context from variable Octopus.TargetDiscovery.Context is in wrong format"));
        log.StandardOut.Should().Contain(line => line.Contains("Aborting target discovery."));
    }

    [Test]
    public async Task Exectute_LogsError_WhenContextIsInIncorrectFormat_AuthenticationMethod()
    {
        // Arrange
        var variables = new CalamariVariables();
        var context = new RunningDeployment(variables);
        context.Variables.Add(TargetDiscoverySpecialVariables.TargetDiscoveryContext, "bogus json");
        var log = new InMemoryLog();
        var sut = new TargetDiscoveryBehaviour(log, DiscovererReturning());

        // Act
        await sut.Execute(context);

        // Assert
        log.StandardOut.Should().Contain(line => line.Contains("Could not read authentication method from target discovery context, Octopus.TargetDiscovery.Context is in wrong format, Unexpected character encountered while parsing value: b. Path '', line 0, position 0."));
        log.StandardOut.Should().Contain(line => line.Contains("Aborting target discovery."));
    }

    [Test]
    public async Task Execute_WebAppWithMatchingTags_CreatesCorrectTargets()
    {
        // Arrange
        var context = ContextWithRoles(WebAppRoleTagValue);
        var discoverer = DiscovererReturning(
            WebApp(WebAppName, TagsFor(role: WebAppRoleTagValue, tenantedMode: TenantedDeploymentModeName)));
        var log = new InMemoryLog();
        var sut = new TargetDiscoveryBehaviour(log, discoverer);

        // Act
        await sut.Execute(context);

        // Assert
        log.StandardOut.Should().Contain(WebAppTargetMessage(WebAppName, WebAppRoleTagValue, slotName: null, tenantedMode: TenantedDeploymentModeName));
    }

    [Test]
    public async Task Execute_WebAppWithNonMatchingTags_CreatesNoTargets()
    {
        // Arrange
        const string role = "a-different-role";
        var context = ContextWithRoles(role);
        var discoverer = DiscovererReturning(
            WebApp(WebAppName, TagsFor(role: WebAppRoleTagValue, tenantedMode: TenantedDeploymentModeName)));
        var log = new InMemoryLog();
        var sut = new TargetDiscoveryBehaviour(log, discoverer);

        // Act
        await sut.Execute(context);

        // Assert
        log.StandardOut.Should().NotContain(WebAppTargetMessage(WebAppName, role, slotName: null, tenantedMode: TenantedDeploymentModeName),
            "the web app target should not be created as the role tag did not match");
    }

    [Test]
    public async Task Execute_MultipleWebAppSlotsWithTags_WebAppHasNoMatchingTags_CreatesTargetsForSlotsOnly()
    {
        // Arrange
        var context = ContextWithRoles(WebAppSlotRoleTagValue);
        var discoverer = DiscovererReturning(
            new[] { WebApp(WebAppName, TagsFor()) }
                .Concat(SlotNames.Select(s => Slot(s, TagsFor(role: WebAppSlotRoleTagValue))))
                .ToArray());
        var log = new InMemoryLog();
        var sut = new TargetDiscoveryBehaviour(log, discoverer);

        // Act
        await sut.Execute(context);

        // Assert
        log.StandardOut.Should().NotContain(WebAppTargetMessage(WebAppName, WebAppRoleTagValue, slotName: null, tenantedMode: null),
            "a target should not be created for the web app itself, only for slots within the web app");
        foreach (var slotName in SlotNames)
        {
            log.StandardOut.Should().Contain(WebAppTargetMessage(WebAppName, WebAppSlotRoleTagValue, slotName, tenantedMode: null));
        }
    }

    [Test]
    public async Task Execute_MultipleWebAppSlotsWithTags_WebAppWithTags_CreatesTargetsForWebAppAndSlots()
    {
        // Arrange
        var context = ContextWithRoles(WebAppRoleTagValue, WebAppSlotRoleTagValue);
        var discoverer = DiscovererReturning(
            new[] { WebApp(WebAppName, TagsFor(role: WebAppRoleTagValue, tenantedMode: TenantedDeploymentModeName)) }
                .Concat(SlotNames.Select(s => Slot(s, TagsFor(role: WebAppSlotRoleTagValue))))
                .ToArray());
        var log = new InMemoryLog();
        var sut = new TargetDiscoveryBehaviour(log, discoverer);

        // Act
        await sut.Execute(context);

        // Assert
        log.StandardOut.Should().Contain(WebAppTargetMessage(WebAppName, WebAppRoleTagValue, slotName: null, tenantedMode: TenantedDeploymentModeName),
            "a target should be created for the web app itself as well as for the slots");
        foreach (var slotName in SlotNames)
        {
            log.StandardOut.Should().Contain(WebAppTargetMessage(WebAppName, WebAppSlotRoleTagValue, slotName, tenantedMode: null));
        }
    }

    [Test]
    public async Task Execute_WebAppAndSlotWithPartialTags_CreatesNoTargets()
    {
        // Arrange
        const string partialMatchSlotName = "partial-match";
        var context = ContextWithRoles(WebAppSlotRoleTagValue);
        var discoverer = DiscovererReturning(
            // Web app has the environment tag but not the role tag - does not match on its own.
            WebApp(WebAppName, TagsFor()),
            // Slot has the role tag but not the environment tag - does not match on its own.
            Slot(partialMatchSlotName, TagsFor(role: WebAppSlotRoleTagValue, environment: null)));
        var log = new InMemoryLog();
        var sut = new TargetDiscoveryBehaviour(log, discoverer);

        // Act
        await sut.Execute(context);

        // Assert
        log.StandardOut.Should().NotContain(WebAppTargetMessage(WebAppName, WebAppRoleTagValue, slotName: null, tenantedMode: TenantedDeploymentModeName),
            "a target should not be created for the web app as the tags directly on the web app do not match");
        log.StandardOut.Should().NotContain(WebAppTargetMessage(WebAppName, WebAppSlotRoleTagValue, partialMatchSlotName, tenantedMode: null),
            "a target should not be created for the slot as the tags directly on the slot do not match");
    }

    static IAzureWebAppDiscoverer DiscovererReturning(params AzureResource[] resources)
    {
        var discoverer = Substitute.For<IAzureWebAppDiscoverer>();
        discoverer.DiscoverWebAppsAndSlots(Arg.Any<IAzureAccount>()).Returns(resources);
        return discoverer;
    }

    static AzureResource WebApp(string name, Dictionary<string, string> tags) =>
        new() { Name = name, Type = "microsoft.web/sites", ResourceGroup = ResourceGroupName, Tags = tags };

    static AzureResource Slot(string slotName, Dictionary<string, string> tags) =>
        new() { Name = $"{WebAppName}/{slotName}", Type = "microsoft.web/sites/slots", ResourceGroup = ResourceGroupName, Tags = tags };

    static Dictionary<string, string> TagsFor(string role = null, string environment = EnvironmentTagValue, string tenantedMode = null)
    {
        var tags = new Dictionary<string, string>();
        if (environment != null) tags[TargetTags.EnvironmentTagName] = environment;
        if (role != null) tags[TargetTags.RoleTagName] = role;
        if (tenantedMode != null) tags[TargetTags.TenantedDeploymentModeTagName] = tenantedMode;
        return tags;
    }

    static string WebAppTargetMessage(string webAppName, string role, string slotName, string tenantedMode) =>
        TargetDiscoveryHelpers.CreateWebAppTargetCreationServiceMessage(
            ResourceGroupName, webAppName, AccountId, role, null, slotName, tenantedMode).ToString();

    static RunningDeployment ContextWithRoles(params string[] roles)
    {
        var variables = new CalamariVariables();
        var context = new RunningDeployment(variables);
        var rolesJson = string.Join(",", roles.Select(r => $"\"{r}\""));

        // Credentials are dummy values - the discoverer that would use them is mocked in these tests.
        var targetDiscoveryContext = $$"""
                                       {
                                           "scope": {
                                               "spaceName": "default",
                                               "environmentName": "{{EnvironmentTagValue}}",
                                               "projectName": "my-test-project",
                                               "tenantName": null,
                                               "roles": [{{rolesJson}}]
                                           },
                                           "authentication": {
                                               "type": "Azure",
                                               "accountId": "{{AccountId}}",
                                               "authenticationMethod": "ServicePrincipal",
                                               "accountDetails": {
                                                   "subscriptionNumber": "subscription-id",
                                                   "clientId": "client-id",
                                                   "tenantId": "tenant-id",
                                                   "password": "client-secret",
                                                   "azureEnvironment": "",
                                                   "resourceManagementEndpointBaseUri": "",
                                                   "activeDirectoryEndpointBaseUri": ""
                                               }
                                           }
                                       }
                                       """;

        context.Variables.Add("Octopus.TargetDiscovery.Context", targetDiscoveryContext);
        return context;
    }
}
