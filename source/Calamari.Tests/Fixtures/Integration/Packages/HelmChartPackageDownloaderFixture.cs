using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.Packages.Download;
using Calamari.Testing;
using Calamari.Testing.Requirements;
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
        static string FeedPassword;
        static string home = Path.GetTempPath();
        HelmChartPackageDownloader downloader;
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;
        
        [OneTimeSetUp]
        public async Task TestFixtureSetUp()
        {
            FeedPassword = await ExternalVariables.Get(ExternalVariable.HelmPassword, cancellationToken);
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
            downloader = new HelmChartPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
        }
        
        [Test]
        [RequiresMonoVersion480OrAboveForTls12(Description = "This test requires TLS 1.2, which doesn't work with mono prior to 4.8")]
        [RequiresMinimumMonoVersion(5, 12, 0, Description = "HttpClient 4.3.2 broken on Mono - https://xamarin.github.io/bugzilla-archives/60/60315/bug.html#c7")]
        public void PackageWithCredentials_Loads()
        {
            var pkg = downloader.DownloadPackage("mychart", new SemanticVersion("0.3.7"), "helm-feed", new Uri(AuthFeedUri), FeedUsername, FeedPassword, true, 1,
                TimeSpan.FromSeconds(3));
            pkg.PackageId.Should().Be("mychart");
            pkg.Version.Should().Be(new SemanticVersion("0.3.7"));
        }
        
        [Test]
        [RequiresMonoVersion480OrAboveForTls12(Description = "This test requires TLS 1.2, which doesn't work with mono prior to 4.8")]
        [RequiresMinimumMonoVersion(5, 12, 0, Description = "HttpClient 4.3.2 broken on Mono - https://xamarin.github.io/bugzilla-archives/60/60315/bug.html#c7")]
        public void PackageWithInvalidUrl_Throws()
        {
            var badUrl = new Uri($"https://octopusdeploy.jfrog.io/gobbelygook/{Guid.NewGuid().ToString("N")}");
            var badEndpointDownloader = new HelmChartPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
            Action action = () => badEndpointDownloader.DownloadPackage("something", new SemanticVersion("99.9.7"), "gobbely", new Uri(badUrl, "something.99.9.7"), FeedUsername, FeedPassword, true, 1, TimeSpan.FromSeconds(3));
            action.Should().Throw<Exception>().And.Message.Should().Contain("Unable to read Helm index file").And.Contain("404");
        }
    }
}