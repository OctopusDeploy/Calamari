using System.Threading.Tasks;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Calamari.Azure.AppServices;
using Calamari.AzureAppService.Azure;
using Calamari.AzureAppService.Behaviors;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using AccountVariables = Calamari.AzureAppService.Azure.AccountVariables;

namespace Calamari.AzureAppService.Tests;

[TestFixture]
public class AzureAppServiceContainerDeployBehaviourUnitTestFixture
{
    const string WebAppName = "my-web-app";
    const string ResourceGroupName = "my-rg";
    const string Registry = "index.docker.io";
    const string Image = "e2eteam/sample-apiserver:1.17";
    const string RegistryUsername = "registry-user";
    const string RegistryPassword = "registry-password";

    [Test]
    public async Task LinuxWebApp_SetsLinuxFxVersionAndRegistrySettings()
    {
        // Arrange
        var config = new SiteConfigData();
        var appSettings = new AppServiceConfigurationDictionary();
        var configurer = ConfigurerReturning(isLinux: true, config, appSettings);
        var sut = new AzureAppServiceContainerDeployBehaviour(new InMemoryLog(), configurer);

        // Act
        await sut.Execute(ContextFor(slotName: null));

        // Assert
        config.LinuxFxVersion.Should().Be($"DOCKER|{Image}");
        config.WindowsFxVersion.Should().BeNull("the Linux FxVersion field should be set, not the Windows one");
        appSettings.Properties["DOCKER_REGISTRY_SERVER_URL"].Should().Be("https://" + Registry);
        appSettings.Properties["DOCKER_REGISTRY_SERVER_USERNAME"].Should().Be(RegistryUsername);
        appSettings.Properties["DOCKER_REGISTRY_SERVER_PASSWORD"].Should().Be(RegistryPassword);
        await configurer.Received(1).UpdateAppSettings(Arg.Any<IAzureAccount>(), Arg.Any<AzureTargetSite>(), appSettings);
        await configurer.Received(1).UpdateSiteConfig(Arg.Any<IAzureAccount>(), Arg.Any<AzureTargetSite>(), config);
    }

    [Test]
    public async Task WindowsWebApp_SetsWindowsFxVersion()
    {
        // Arrange
        var config = new SiteConfigData();
        var configurer = ConfigurerReturning(isLinux: false, config, new AppServiceConfigurationDictionary());
        var sut = new AzureAppServiceContainerDeployBehaviour(new InMemoryLog(), configurer);

        // Act
        await sut.Execute(ContextFor(slotName: null));

        // Assert
        config.WindowsFxVersion.Should().Be($"DOCKER|{Image}");
        config.LinuxFxVersion.Should().BeNull("the Windows FxVersion field should be set, not the Linux one");
    }

    [Test]
    public async Task SlotDeploy_TargetsTheSlot()
    {
        // Arrange
        var configurer = ConfigurerReturning(isLinux: true, new SiteConfigData(), new AppServiceConfigurationDictionary());
        var sut = new AzureAppServiceContainerDeployBehaviour(new InMemoryLog(), configurer);

        // Act
        await sut.Execute(ContextFor(slotName: "stage"));

        // Assert
        await configurer.Received().UpdateSiteConfig(
            Arg.Any<IAzureAccount>(),
            Arg.Is<AzureTargetSite>(t => t.HasSlot && t.Slot == "stage"),
            Arg.Any<SiteConfigData>());
    }

    [Test]
    public async Task NonSlotDeploy_TargetsTheWebAppItself()
    {
        // Arrange
        var configurer = ConfigurerReturning(isLinux: true, new SiteConfigData(), new AppServiceConfigurationDictionary());
        var sut = new AzureAppServiceContainerDeployBehaviour(new InMemoryLog(), configurer);

        // Act
        await sut.Execute(ContextFor(slotName: null));

        // Assert
        await configurer.Received().UpdateSiteConfig(
            Arg.Any<IAzureAccount>(),
            Arg.Is<AzureTargetSite>(t => !t.HasSlot && t.Site == WebAppName),
            Arg.Any<SiteConfigData>());
    }

    static IAzureAppServiceContainerConfigurer ConfigurerReturning(bool isLinux, SiteConfigData config, AppServiceConfigurationDictionary appSettings)
    {
        var configurer = Substitute.For<IAzureAppServiceContainerConfigurer>();
        configurer.IsLinuxWebApp(Arg.Any<IAzureAccount>(), Arg.Any<AzureTargetSite>()).Returns(isLinux);
        configurer.GetSiteConfig(Arg.Any<IAzureAccount>(), Arg.Any<AzureTargetSite>()).Returns(config);
        configurer.GetAppSettings(Arg.Any<IAzureAccount>(), Arg.Any<AzureTargetSite>()).Returns(appSettings);
        return configurer;
    }

    static RunningDeployment ContextFor(string slotName)
    {
        var variables = new CalamariVariables();

        // Dummy credentials - the configurer that would use them to reach Azure is mocked in these tests.
        variables.Add(AccountVariables.SubscriptionId, "subscription-id");
        variables.Add(AccountVariables.ClientId, "client-id");
        variables.Add(AccountVariables.TenantId, "tenant-id");
        variables.Add(AccountVariables.Password, "client-secret");

        variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, ResourceGroupName);
        variables.Add(SpecialVariables.Action.Azure.WebAppName, WebAppName);
        if (slotName != null)
            variables.Add(SpecialVariables.Action.Azure.WebAppSlot, slotName);

        variables.Add(SpecialVariables.Action.Package.Image, Image);
        variables.Add(SpecialVariables.Action.Package.Registry, Registry);
        variables.Add(SpecialVariables.Action.Package.Feed.Username, RegistryUsername);
        variables.Add(SpecialVariables.Action.Package.Feed.Password, RegistryPassword);

        return new RunningDeployment("", variables);
    }
}