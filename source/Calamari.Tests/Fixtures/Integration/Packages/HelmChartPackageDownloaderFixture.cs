using System;
using System.IO;
using System.Net;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Download;
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
        private static string home = Path.GetTempPath();
        
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
        
        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        public void PackageWithCredentials_Loads()
        {
            var downloader = new HelmChartPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
            var pkg = downloader.DownloadPackage("mychart", new SemanticVersion("0.3.7"), "helm-feed", new Uri(AuthFeedUri), new NetworkCredential(FeedUsername, FeedPassword), true, 1,
                TimeSpan.FromSeconds(3));
            
            Assert.AreEqual("mychart", pkg.PackageId);
            Assert.AreEqual(new SemanticVersion("0.3.7"), pkg.Version);
        }        
        
        [Test]
        [RequiresNonFreeBSDPlatform]
        [RequiresNon32BitWindows]
        [RequiresNonMacAttribute]
        public void PackageWithWrongCredentials_Fails()
        {
            var downloader = new HelmChartPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
            var exception = Assert.Throws<CommandException>(() => downloader.DownloadPackage("mychart", new SemanticVersion("0.3.7"), "helm-feed", new Uri(AuthFeedUri), new NetworkCredential(FeedUsername, "FAKE"), true, 1,
                TimeSpan.FromSeconds(3)));
            
            StringAssert.Contains("Helm failed to download the chart", exception.Message);
            StringAssert.Contains("401 Unauthorized", exception.Message);
        }
    }
}