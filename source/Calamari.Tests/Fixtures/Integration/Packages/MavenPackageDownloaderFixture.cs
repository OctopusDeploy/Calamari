using System;
using System.Net;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Integration.Packages.Download;
using Calamari.Tests.Fixtures.PackageRetention.Repository;
using Calamari.Tests.Helpers;
using NSubstitute;
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
            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage("com.google.guava:guava", VersionFactory.CreateMavenVersion("22.0"), "feed-maven",
                new Uri("https://repo.maven.apache.org/maven2/"), "", "", true, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("com.google.guava:guava", pkg.PackageId);
        }
        
        [Test]
        [RequiresMonoVersion480OrAboveForTls12]
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
            return new MavenPackageDownloader(CalamariPhysicalFileSystem.GetPhysicalFileSystem(),
                                              new Journal(
                                                          new InMemoryJournalRepositoryFactory(),
                                                          Substitute.For<ILog>(),
                                                          Substitute.For<ICalamariFileSystem>(),
                                                          Substitute.For<IRetentionAlgorithm>(),
                                                          Substitute.For<IVariables>(),
                                                          Substitute.For<IFreeSpaceChecker>()
                                                         )
                                             );
        }
    }
}
