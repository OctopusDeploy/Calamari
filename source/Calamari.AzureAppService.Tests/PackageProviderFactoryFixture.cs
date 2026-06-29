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
    [TestCase(".zip", "/api/zipdeploy", true)]
    [TestCase(".nupkg", "/api/zipdeploy", true)]
    [TestCase(".war", "/api/wardeploy", false)]
    [TestCase(".jar", "/api/publish?type=jar", false)]
    public void GetProvider_SelectsCorrectUploadEndpointAndDeploymentMode(string extension, string expectedUploadUrlPath, bool expectedSupportsAsync)
    {
        var provider = PackageProviderFactory.GetProvider(extension, new InMemoryLog(), Substitute.For<ICalamariFileSystem>(), new CalamariVariables(), DeploymentContext());

        provider.UploadUrlPath.Should().Be(expectedUploadUrlPath);
        provider.SupportsAsynchronousDeployment.Should().Be(expectedSupportsAsync);
    }

    [Test]
    public void GetProvider_ThrowsForUnsupportedExtension()
    {
        Action act = () => PackageProviderFactory.GetProvider(".rpm", new InMemoryLog(), Substitute.For<ICalamariFileSystem>(), new CalamariVariables(), DeploymentContext());

        act.Should().Throw<Exception>().WithMessage("Unsupported archive type");
    }

    static RunningDeployment DeploymentContext() => new("", new CalamariVariables());
}