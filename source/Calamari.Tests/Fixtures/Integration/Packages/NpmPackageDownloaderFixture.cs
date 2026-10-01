using System;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.Download;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class NpmPackageDownloaderFixture
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
        public void DownloadNpmPackage()
        {
            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage("express", VersionFactory.CreateSemanticVersion("4.18.2"), "feed-npm",
                new Uri("https://registry.npmjs.org"), "", "", true, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("express", pkg.PackageId);
            Assert.AreEqual(".tgz", pkg.Extension);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        public void DownloadScopedNpmPackage()
        {
            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage("@types/node", VersionFactory.CreateSemanticVersion("20.0.0"), "feed-npm",
                new Uri("https://registry.npmjs.org"), "", "", true, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("@types/node", pkg.PackageId);
            Assert.AreEqual(".tgz", pkg.Extension);
        }

        [Test]
        [RequiresNonFreeBSDPlatform]
        public void DownloadNpmPackageFromCache()
        {
            var downloader = GetDownloader();

            // First download with force=true to ensure we get a fresh download
            var pkg1 = downloader.DownloadPackage("lodash", VersionFactory.CreateSemanticVersion("4.17.21"), "feed-npm",
                new Uri("https://registry.npmjs.org"), "", "", true, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("lodash", pkg1.PackageId);
            Assert.AreEqual(".tgz", pkg1.Extension);

            // Second download with force=false should use cache
            var pkg2 = downloader.DownloadPackage("lodash", VersionFactory.CreateSemanticVersion("4.17.21"), "feed-npm",
                new Uri("https://registry.npmjs.org"), "", "", false, 3, TimeSpan.FromSeconds(3));

            Assert.AreEqual("lodash", pkg2.PackageId);
            Assert.AreEqual(".tgz", pkg2.Extension);
            // Verify it's the same file (comparing directory, package ID, version - not the random cache buster)
            Assert.AreEqual(Path.GetDirectoryName(pkg1.FullFilePath), Path.GetDirectoryName(pkg2.FullFilePath));
        }

        static NpmPackageDownloader GetDownloader()
        {
            var log = Substitute.For<ILog>();
            return new NpmPackageDownloader(log, CalamariPhysicalFileSystem.GetPhysicalFileSystem());
        }
    }
}
