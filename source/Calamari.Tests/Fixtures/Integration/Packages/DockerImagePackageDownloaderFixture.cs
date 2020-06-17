using System;
using System.IO;
using System.Linq;
using System.Net;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages.Download;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Variables;
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
        [RequiresDockerInstalledAttribute]
        public void PackageWithoutCredentials_Loads()
        {
            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage("alpine", 
                new SemanticVersion("3.6.5"), "docker-feed", 
                new Uri(DockerHubFeedUri), null, true, 1,
                TimeSpan.FromSeconds(3));

            Assert.AreEqual("alpine", pkg.PackageId);
            Assert.AreEqual(new SemanticVersion("3.6.5"), pkg.Version);
            Assert.AreEqual(string.Empty, pkg.FullFilePath);
        }

        [Test]
        [RequiresDockerInstalledAttribute]
        public void DockerHubWithCredentials_Loads()
        {
            const string privateImage = "octopusdeploy/octo-prerelease";
            var version =  new SemanticVersion("7.3.7-alpine");

            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage(privateImage, 
                version, 
                "docker-feed", 
                new Uri(DockerHubFeedUri),
                new NetworkCredential(DockerTestUsername, DockerTestPassword), 
                true, 
                1,
                TimeSpan.FromSeconds(3));

            Assert.AreEqual(privateImage, pkg.PackageId);
            Assert.AreEqual(version, pkg.Version);
            Assert.AreEqual(string.Empty, pkg.FullFilePath);
        }

        [Test]
        [RequiresDockerInstalledAttribute]
        public void PackageWithCredentials_Loads()
        {
            var downloader = GetDownloader();
            var pkg = downloader.DownloadPackage("octopus-echo",
                new SemanticVersion("1.1"), 
                "docker-feed",
                new Uri(AuthFeedUri), 
                new NetworkCredential(FeedUsername, FeedPassword), true, 1,
                TimeSpan.FromSeconds(3));

            Assert.AreEqual("octopus-echo", pkg.PackageId);
            Assert.AreEqual(new SemanticVersion("1.1"), pkg.Version);
            Assert.AreEqual(string.Empty, pkg.FullFilePath);
        }

        [Test]
        [RequiresDockerInstalledAttribute]
        public void PackageWithWrongCredentials_Fails()
        {
            var downloader = GetDownloader();
            var exception = Assert.Throws<CommandException>(() => downloader.DownloadPackage("octopus-echo", 
                new SemanticVersion("1.1"), "docker-feed", 
                new Uri(AuthFeedUri), 
                new NetworkCredential(FeedUsername, "SuperDooper"), true, 1,
                TimeSpan.FromSeconds(3)));

            StringAssert.Contains("Unable to pull Docker image", exception.Message);
        }

        DockerImagePackageDownloader GetDownloader()
        {
            var runner = new CommandLineRunner(ConsoleLog.Instance, new CalamariVariables());
            return new DockerImagePackageDownloader(new ScriptEngine(Enumerable.Empty<IScriptWrapper>()), CalamariPhysicalFileSystem.GetPhysicalFileSystem(), runner, new CalamariVariables());
        }
    }
}
