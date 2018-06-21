using System;
using System.Net;
using Calamari.Integration.Packages.Download;
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
        [RequiresMonoVersion480OrAbove]
        public void Blah()
        {
            var downloader = new MavenPackageDownloader();
            var pkg = downloader.DownloadPackage("com.google.guava:guava", VersionFactory.CreateMavenVersion("22.0"), "feed-maven",
                new Uri("https://repo.maven.apache.org/maven2/"), new NetworkCredential("", ""), true, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("com.google.guava:guava", pkg.PackageId);
        }
    }
}
