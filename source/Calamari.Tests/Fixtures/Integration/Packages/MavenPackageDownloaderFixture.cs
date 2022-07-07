using System;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.Packages.Download;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class MavenPackageDownloaderFixture
    {
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "PackageDownload");

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", TentacleHome);
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        public void DownloadMavenPackage()
        {
            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage("com.google.guava:guava", VersionFactory.CreateMavenVersion("22.0"), "feed-maven",
                new Uri("https://repo.maven.apache.org/maven2/"), "", "", true, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("com.google.guava:guava", pkg.PackageId);
        }
        
        [Test]
        [RequiresNonFreeBSDPlatform]
        public void DownloadMavenSourcePackage()
        {
            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage("com.google.guava:guava:jar:sources", VersionFactory.CreateMavenVersion("22.0"), "feed-maven",
                new Uri("https://repo.maven.apache.org/maven2/"), "", "", true, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("com.google.guava:guava:jar:sources", pkg.PackageId);
        }

        static MavenPackageDownloader GetDownloader()
        {
            return new MavenPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
        }
    }
}
