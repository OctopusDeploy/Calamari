using System;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Integration.Packages.Download;
using Calamari.Testing;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
#if NETFX
    [Ignore("GitHub tests are not run in .netcore to reduce throttling exceptions from GitHub itself.")]
#endif
    public class GitHubPackageDownloadFixture
    {
        //See "GitHub Test Account"
        static readonly string AuthFeedUri =  "https://api.github.com";
        static readonly string FeedUsername = ExternalVariables.Get(ExternalVariable.GitHubUsername);
        static readonly string FeedPassword = ExternalVariables.Get(ExternalVariable.GitHubPassword);

        static readonly CalamariPhysicalFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

        static string home = Path.GetTempPath();
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
        public void SetUp()
        {
            var rootDir = new PackageDownloaderUtils().RootDirectory;
            if (Directory.Exists(rootDir))
                Directory.Delete(rootDir, true);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)] //Keeps rate limit low
        public void DownloadsPackageFromGitHub()
        {
            var downloader = GetDownloader();

            var file = downloader.DownloadPackage("OctopusDeploy/Octostache", new SemanticVersion("2.1.8"), "feed-github",
                new Uri(AuthFeedUri), FeedUsername, FeedPassword,
                true, 3,
                TimeSpan.FromSeconds(3));

            Assert.Greater(file.Size, 0);
            Assert.IsFalse(String.IsNullOrWhiteSpace(file.Hash));
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)] //Keeps rate limit low
        public void WillReUseFileIfItExists()
        {
            var downloader = GetDownloader();

            var file1 = downloader.DownloadPackage("OctopusDeploy/Octostache", new SemanticVersion("2.1.7"), "feed-github",
                new Uri(AuthFeedUri), FeedUsername, FeedPassword, true, 3,
                TimeSpan.FromSeconds(3));

            Assert.Greater(file1.Size, 0);

            var file2 = downloader.DownloadPackage("OctopusDeploy/Octostache", new SemanticVersion("2.1.7"), "feed-github",
                new Uri("https://WillFailIfInvoked"), null, null, false, 3,
                TimeSpan.FromSeconds(3));

            Assert.AreEqual(file1.FullFilePath, file1.FullFilePath);
            Assert.AreEqual(file1.Hash, file2.Hash);
            Assert.AreEqual(file1.Size, file1.Size);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)] //Keeps rate limit low
        public void DownloadsPackageFromGitHubWithDifferentVersionFormat()
        {
            var downloader = GetDownloader();

            var file = downloader.DownloadPackage("octokit/octokit.net", new SemanticVersion("0.28.0"), "feed-github",
                new Uri(AuthFeedUri), FeedUsername, FeedPassword, true, 3,
                TimeSpan.FromSeconds(3));

            Assert.Greater(file.Size, 0);
            Assert.IsFalse(String.IsNullOrWhiteSpace(file.Hash));
        }

        static GitHubPackageDownloader GetDownloader()
        {
            return new GitHubPackageDownloader(new InMemoryLog(), fileSystem);
        }
    }
}
