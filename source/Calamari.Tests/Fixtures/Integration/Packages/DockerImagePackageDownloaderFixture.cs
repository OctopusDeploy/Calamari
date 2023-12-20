using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.Download;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class DockerImagePackageDownloaderFixture
    {
        static readonly string AuthFeedUri =   "https://octopusdeploy-docker.jfrog.io";
        static readonly string FeedUsername = "e2e-reader";
        static readonly string FeedPassword = ExternalVariables.Get(ExternalVariable.HelmPassword);
        static readonly string Home = Path.GetTempPath();

        static readonly string DockerHubFeedUri = "https://index.docker.io";
        static readonly string DockerTestUsername = "octopustestaccount";
        static readonly string DockerTestPassword = ExternalVariables.Get(ExternalVariable.DockerReaderPassword);

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", Home);
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [Test]
        [RequiresDockerInstalled]
        public void PackageWithoutCredentials_Loads()
        {
            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage("alpine",
                new SemanticVersion("3.6.5"), "docker-feed",
                new Uri(DockerHubFeedUri), null, null, true, 1,
                TimeSpan.FromSeconds(3));

            Assert.AreEqual("alpine", pkg.PackageId);
            Assert.AreEqual(new SemanticVersion("3.6.5"), pkg.Version);
            Assert.AreEqual(string.Empty, pkg.FullFilePath);
        }

        [Test]
        [RequiresDockerInstalled]
        public void DockerHubWithCredentials_Loads()
        {
            const string privateImage = "octopusdeploy/octo-prerelease";
            var version =  new SemanticVersion("7.3.7-alpine");

            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage(privateImage,
                version,
                "docker-feed",
                new Uri(DockerHubFeedUri),
                DockerTestUsername, DockerTestPassword,
                true,
                1,
                TimeSpan.FromSeconds(3));

            Assert.AreEqual(privateImage, pkg.PackageId);
            Assert.AreEqual(version, pkg.Version);
            Assert.AreEqual(string.Empty, pkg.FullFilePath);
        }

        [Test]
        [RequiresDockerInstalled]
        public void PackageWithCredentials_Loads()
        {
            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage("octopus-echo",
                new SemanticVersion("1.1"),
                "docker-feed",
                new Uri(AuthFeedUri),
                FeedUsername, FeedPassword,
                true, 1,
                TimeSpan.FromSeconds(3));

            Assert.AreEqual("octopus-echo", pkg.PackageId);
            Assert.AreEqual(new SemanticVersion("1.1"), pkg.Version);
            Assert.AreEqual(string.Empty, pkg.FullFilePath);
        }

        [Test]
        [RequiresDockerInstalled]
        public void PackageWithWrongCredentials_Fails()
        {
            var downloader = GetDownloader();
            var exception = Assert.Throws<CommandException>(() => downloader.DownloadPackage("octopus-echo",
                new SemanticVersion("1.1"), "docker-feed",
                new Uri(AuthFeedUri),
                FeedUsername, "SuperDooper",
                true, 1,
                TimeSpan.FromSeconds(3)));

            StringAssert.Contains("Unable to log in Docker registry", exception.Message);
        }

        [Test]
        [RequiresDockerInstalled]
        [TestCase("octopusdeploy/octo-prerelease", "7.3.7-alpine")]
        [TestCase("alpine", "3.6.5")]
        public void CachedDockerHubPackage_DoesNotGenerateImageNotCachedMessage(string image, string tag)
        {
            PreCacheImage(image, tag, DockerHubFeedUri, DockerTestUsername, DockerTestPassword);
            
            var log = new InMemoryLog();
            var downloader = GetDownloader(log);
            downloader.DownloadPackage(image, 
                                       new SemanticVersion(tag), 
                                       "docker-feed", 
                                       new Uri(DockerHubFeedUri), 
                                       DockerTestUsername, 
                                       DockerTestPassword, 
                                       true, 
                                       1, 
                                       TimeSpan.FromSeconds(3));

            Assert.False(log.Messages.Any(m => m.FormattedMessage.Contains($"The docker image '{image}:{tag}' may not be cached")));
        }
        
        [Test]
        [RequiresDockerInstalled]
        public void CachedNonDockerHubPackage_DoesNotGenerateImageNotCachedMessage()
        {
            const string image = "octopus-echo";
            const string tag = "1.1";
            var log = new InMemoryLog();
            var downloader = GetDownloader(log);

            PreCacheImage(image, tag, AuthFeedUri, FeedUsername, FeedPassword);

            downloader.DownloadPackage(image, 
                                       new SemanticVersion(tag), 
                                       "docker-feed", 
                                       new Uri(AuthFeedUri), 
                                       FeedUsername,
                                       FeedPassword,  
                                       true, 
                                       1, 
                                       TimeSpan.FromSeconds(3));

            Assert.False(log.Messages.Any(m => m.FormattedMessage.Contains($"The docker image '{image}:{tag}' may not be cached")));
        }
        
        [Test]
        [RequiresDockerInstalled]
        [TestCase("octopusdeploy/octo-prerelease", "7.3.7-alpine")]
        [TestCase("alpine", "3.6.5")]
        public void NotCachedDockerHubPackage_GeneratesImageNotCachedMessage(string image, string tag)
        {
            var log = new InMemoryLog();
            var downloader = GetDownloader(log);
            
            RemoveCachedImage(image, tag);

            downloader.DownloadPackage(image, 
                                       new SemanticVersion(tag), 
                                       "docker-feed", 
                                       new Uri(DockerHubFeedUri), 
                                       DockerTestUsername, 
                                       DockerTestPassword, 
                                       true, 
                                       1, 
                                       TimeSpan.FromSeconds(3));

            Assert.True(log.Messages.Any(m => m.FormattedMessage.Contains($"The docker image '{image}:{tag}' may not be cached")));
        }
        
        [Test]
        [RequiresDockerInstalled]
        public void NotCachedNonDockerHubPackage_GeneratesImageNotCachedMessage()
        {
            const string image = "octopus-echo";
            const string tag = "1.1";
            var feed = new Uri(AuthFeedUri);
            var imageFullName = $"{feed.Authority}/{image}";
            var log = new InMemoryLog();
            var downloader = GetDownloader(log);
            
            RemoveCachedImage(imageFullName, tag);

            downloader.DownloadPackage(image, 
                                       new SemanticVersion(tag), 
                                       "docker-feed", 
                                       feed, 
                                       FeedUsername, 
                                       FeedPassword, 
                                       true, 
                                       1, 
                                       TimeSpan.FromSeconds(3));

            Assert.True(log.Messages.Any(m => m.FormattedMessage.Contains($"The docker image '{imageFullName}:{tag}' may not be cached")));
        }

        static void PreCacheImage(string packageId, string tag, string feedUri, string username, string password)
        {
            GetDownloader(new SilentLog()).DownloadPackage(packageId, 
                                                           new SemanticVersion(tag), 
                                                           "docker-feed", 
                                                           new Uri(feedUri), 
                                                           username, 
                                                           password, 
                                                           true, 
                                                           1, 
                                                           TimeSpan.FromSeconds(3));
        }

        static void RemoveCachedImage(string image, string tag)
        {
            SilentProcessRunner.ExecuteCommand("docker", 
                                               $"rmi {image}:{tag}",
                                               ".", 
                                               new Dictionary<string, string>(),
                                               (output) => { },
                                               (error) => { });
        }

        static DockerImagePackageDownloader GetDownloader()
        {
            return GetDownloader(ConsoleLog.Instance);
        }

        static DockerImagePackageDownloader GetDownloader(ILog log)
        {
            var runner = new CommandLineRunner(log, new CalamariVariables());
            return new DockerImagePackageDownloader(new ScriptEngine(Enumerable.Empty<IScriptWrapper>(), log), CalamariPhysicalFileSystem.GetPhysicalFileSystem(), runner, new CalamariVariables(), log);
        }
    }
}
