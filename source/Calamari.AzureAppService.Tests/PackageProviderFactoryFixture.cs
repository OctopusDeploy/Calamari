using System;
using Calamari.AzureAppService;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests;

[TestFixture]
public class PackageProviderFactoryFixture
{
    // The Kudu upload endpoint and sync/async behaviour are the only things that differ between package
    // types (notably .war and .jar route to the same JavaPackageProvider, differing solely by upload URL).
    // On a Flex Consumption plan, zip/nupkg packages must use the OneDeploy (/api/publish) endpoint instead
    // of Kudu ZipDeploy (/api/zipdeploy), which Flex Consumption does not expose (returns 404).
    [TestCase(".zip", false, "/api/zipdeploy", true, "application/octet-stream")]
    [TestCase(".nupkg", false, "/api/zipdeploy", true, "application/octet-stream")]
    [TestCase(".zip", true, "/api/publish?type=zip", false, "application/zip")]
    [TestCase(".nupkg", true, "/api/publish?type=zip", false, "application/zip")]
    [TestCase(".war", false, "/api/wardeploy", false, "application/octet-stream")]
    [TestCase(".war", true, "/api/wardeploy", false, "application/octet-stream")]
    [TestCase(".jar", false, "/api/publish?type=jar", false, "application/octet-stream")]
    [TestCase(".jar", true, "/api/publish?type=jar", false, "application/octet-stream")]
    public void GetProvider_SelectsCorrectUploadEndpointAndDeploymentMode(string extension, bool isFlexConsumption, string expectedUploadUrlPath, bool expectedSupportsAsync, string expectedContentType)
    {
        var provider = PackageProviderFactory.GetProvider(extension, isFlexConsumption, new InMemoryLog(), Substitute.For<ICalamariFileSystem>(), new CalamariVariables(), DeploymentContext());

        provider.UploadUrlPath.Should().Be(expectedUploadUrlPath);
        provider.SupportsAsynchronousDeployment.Should().Be(expectedSupportsAsync);
        provider.ContentType.Should().Be(expectedContentType);
    }

    [Test]
    public void GetProvider_ThrowsForUnsupportedExtension()
    {
        Action act = () => PackageProviderFactory.GetProvider(".rpm", false, new InMemoryLog(), Substitute.For<ICalamariFileSystem>(), new CalamariVariables(), DeploymentContext());

        act.Should().Throw<Exception>().WithMessage("Unsupported archive type");
    }

    static RunningDeployment DeploymentContext() => new("", new CalamariVariables());
}
