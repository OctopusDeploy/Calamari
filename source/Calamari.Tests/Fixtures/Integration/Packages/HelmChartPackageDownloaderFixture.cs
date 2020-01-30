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
        public void PackageWithCredentials_Loads()
        {
            var pkg = downloader.DownloadPackage("mychart", new SemanticVersion("0.3.7"), "helm-feed", new Uri(AuthFeedUri), new NetworkCredential(FeedUsername, FeedPassword), true, 1,
                TimeSpan.FromSeconds(3));
            pkg.PackageId.Should().Be("mychart");
            pkg.Version.Should().Be(new SemanticVersion("0.3.7"));
        }        
        
        [Test]
        public void PackageWithWrongCredentials_Fails()
        {
            Action download = () => downloader.DownloadPackage("mychart", new SemanticVersion("0.3.7"), "helm-feed",
                new Uri(AuthFeedUri), new NetworkCredential(FeedUsername, "FAKE"), true, 1,
                TimeSpan.FromSeconds(3));
            download.Should().Throw<CommandException>()
                .And.Message.Should().Contain("Helm failed to download the chart");
        }
    }
}