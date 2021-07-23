using System;
using System.Net;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Download;
using Calamari.Integration.Processes;
using Calamari.Tests.Fixtures.Integration.FileSystem;
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
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresNonFreeBSDPlatform]
        public void DownloadMavenPackage()
        {
            var downloader = new MavenPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new FreeSpaceChecker(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new CalamariVariables()));
            var pkg = downloader.DownloadPackage("com.google.guava:guava", VersionFactory.CreateMavenVersion("22.0"), "feed-maven",
                new Uri("https://repo.maven.apache.org/maven2/"), "", "", true, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("com.google.guava:guava", pkg.PackageId);
        }
        
        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
        [RequiresNonFreeBSDPlatform]
        public void DownloadMavenSourcePackage()
        {
            var downloader = new MavenPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new FreeSpaceChecker(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new CalamariVariables()));
            var pkg = downloader.DownloadPackage("com.google.guava:guava:jar:sources", VersionFactory.CreateMavenVersion("22.0"), "feed-maven",
                new Uri("https://repo.maven.apache.org/maven2/"), "", "", true, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("com.google.guava:guava:jar:sources", pkg.PackageId);
        }
    }
}
