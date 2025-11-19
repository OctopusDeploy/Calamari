using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.Packages.Download;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class HelmChartPackageDownloaderFixture
    {
        static string authFeedUri;
        static string feedUsername;
        static string feedPassword;
        static string home = Path.GetTempPath();
        HelmChartPackageDownloader downloader;
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;
        
        [OneTimeSetUp]
        public async Task TestFixtureSetUp()
        {
            var baseUrl = await ExternalVariables.Get(ExternalVariable.ArtifactoryUrl, cancellationToken); 
            authFeedUri =  $"{baseUrl}/octopusdeploy/helm-testing/";
            feedUsername = await ExternalVariables.Get(ExternalVariable.ArtifactoryUsername, cancellationToken);
            feedPassword = await ExternalVariables.Get(ExternalVariable.ArtifactoryPassword, cancellationToken);
            Environment.SetEnvironmentVariable("TentacleHome", home);
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [SetUp]
        public void Setup()
        {
            downloader = new HelmChartPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new InMemoryLog());
        }
        
        [Test]
        public void PackageWithCredentials_Loads()
        {
            var pkg = downloader.DownloadPackage("mychart", new SemanticVersion("0.3.7"), "helm-feed", new Uri(authFeedUri), feedUsername, feedPassword, true, 1,
                TimeSpan.FromSeconds(3));
            pkg.PackageId.Should().Be("mychart");
            pkg.Version.Should().Be(new SemanticVersion("0.3.7"));
        }
        
        [Test]
        public async Task PackageWithInvalidUrl_Throws()
        {
            var baseUrl = await ExternalVariables.Get(ExternalVariable.ArtifactoryUrl, cancellationToken);
            var badUrl = new Uri($"{baseUrl}/gobbelygook/{Guid.NewGuid().ToString("N")}");
            var badEndpointDownloader = new HelmChartPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new InMemoryLog());
            Action action = () => badEndpointDownloader.DownloadPackage("something", new SemanticVersion("99.9.7"), "gobbely", new Uri(badUrl, "something.99.9.7"), feedUsername, feedPassword, true, 1, TimeSpan.FromSeconds(3));
            action.Should().Throw<Exception>().And.Message.Should().Contain("Unable to read Helm index file").And.Contain("404");
        }
    }
}
