using System;
using System.Threading.Tasks;
using Calamari.AzureAppService.Azure;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests.ExternalCloudIntegration;

// Smoke test: the real Azure Resource Graph query and auth still work.
// The tag-matching logic is unit-tested in TargetDiscoveryBehaviourUnitTestFixture.
[TestFixture]
public class TargetDiscoveryBehaviourWithStaticResourcesTestFixture : AzureAppServiceWithStaticResourcesTestBase
{
    // https://portal.azure.com/#@octopusdeploy.onmicrosoft.com/resource/subscriptions/cf21dc34-73dc-4d7d-bd86-041884e0bc75/resourcegroups/calamari-testing-static-rg/providers/Microsoft.Web/sites/calamari-testing-static-target-discovery/appServices
    const string WebAppName = "calamari-testing-static-target-discovery";

    [Test]
    public async Task DiscoverWebAppsAndSlots_ReturnsTheStaticTestWebApp()
    {
        // Arrange
        var discoverer = new AzureWebAppDiscoverer();

        // Act
        var resources = await discoverer.DiscoverWebAppsAndSlots(ServicePrincipalAccount);

        // Assert
        resources.Should().Contain(r => r.Name == WebAppName && r.ResourceGroup == ResourceGroupName,
            "the real Azure Resource Graph query should return the statically provisioned test web app");
    }
}
