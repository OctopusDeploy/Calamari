using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Download;
using Calamari.Integration.Packages.Download.Helm;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class HelmChartPackageDownloaderFixture
    {
        static readonly string AuthFeedUri =   "https://octopusdeploy.jfrog.io/octopusdeploy/helm-testing/";
        static readonly string FeedUsername = "e2e-reader";
        static readonly string FeedPassword = ExternalVariables.Get(ExternalVariable.HelmPassword);
        static string home = Path.GetTempPath();
        HelmChartPackageDownloader downloader;
        
        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
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
            downloader = new HelmChartPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new HelmEndpointProxy(new HttpClient(),new Uri(AuthFeedUri), FeedUsername, FeedPassword), new HttpClient());
        }
        
        [Test]
        [RequiresMonoVersion480OrAbove(Description = "This test requires TLS 1.2, which doesn't work with mono prior to 4.8")]
        public void PackageWithCredentials_Loads()
        {
            var pkg = downloader.DownloadPackage("mychart", new SemanticVersion("0.3.7"), "helm-feed", new Uri(AuthFeedUri), new NetworkCredential(FeedUsername, FeedPassword), true, 1,
                TimeSpan.FromSeconds(3));
            pkg.PackageId.Should().Be("mychart");
            pkg.Version.Should().Be(new SemanticVersion("0.3.7"));
        }
        
        [Test]
        [RequiresMonoVersion480OrAbove(Description = "This test requires TLS 1.2, which doesn't work with mono prior to 4.8")]
        public void PackageWithInvalidUrl_Throws()
        {
            var badUrl = new Uri($"https://octopusdeploy.jfrog.io/gobbelygook/{Guid.NewGuid().ToString("N")}");
            var badEndpointDownloader = new HelmChartPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new HelmEndpointProxy(new HttpClient(), badUrl, FeedUsername, FeedPassword), new HttpClient());
            Action action = () => badEndpointDownloader.DownloadPackage("something", new SemanticVersion("99.9.7"), "gobbely", new Uri(badUrl, "something.99.9.7"), new NetworkCredential(FeedUsername, FeedPassword), true, 1, TimeSpan.FromSeconds(3));
            action.Should().Throw<Exception>().And.Message.Should().Contain("Unable to read Helm index file").And.Contain("404");
        }
    }
}