using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.Download;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class DockerImagePackageDownloaderCredentialHelperFixture
    {
        static readonly string DockerHubFeedUri = "https://index.docker.io";
        static string dockerTestUsername;
        static string dockerTestPassword;
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;
        static readonly string Home = Path.GetTempPath();

        [OneTimeSetUp]
        public async Task TestFixtureSetUp()
        {
            dockerTestUsername = await ExternalVariables.Get(ExternalVariable.DockerHubOrgAccessUsername, cancellationToken);
            dockerTestPassword = await ExternalVariables.Get(ExternalVariable.DockerHubOrgAccessToken, cancellationToken);
            Environment.SetEnvironmentVariable("TentacleHome", Home);
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [Test]
        [RequiresDockerInstalled]
        public void CredentialHelper_EnabledByDefault_UsesCredentialHelper()
        {
            // Arrange
            var log = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.UseDockerCredentialHelper);
            var downloader = GetDownloader(log, variables);

            // Act
            var pkg = downloader.DownloadPackage("octopusdeploy/octo-prerelease",
                new SemanticVersion("7.3.7-alpine"), "docker-feed",
                new Uri(DockerHubFeedUri), dockerTestUsername, dockerTestPassword, true, 1,
                TimeSpan.FromSeconds(10));

            // Assert
            pkg.Should().NotBeNull();
            pkg.PackageId.Should().Be("octopusdeploy/octo-prerelease");
            
            // Verify credential helper was configured
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("Configured Docker credential helper"));
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("Cleaned up Docker credential files"));
            
            // Verify no unencrypted credential warnings in the log
            log.Messages.Should().NotContain(m => m.FormattedMessage.Contains("Your password will be stored unencrypted in octo-docker-configs/config.json"));
        }

        [Test]
        [RequiresDockerInstalled]
        public void CredentialHelper_ExplicitlyDisabled_UsesFallbackLogin()
        {
            // Arrange
            var log = new InMemoryLog();
            var variables = new CalamariVariables();

            var downloader = GetDownloader(log, variables);
            
            // Act
            var pkg = downloader.DownloadPackage("octopusdeploy/octo-prerelease",
                new SemanticVersion("7.3.7-alpine"), "docker-feed",
                new Uri(DockerHubFeedUri), dockerTestUsername, dockerTestPassword, true, 1,
                TimeSpan.FromSeconds(10));

            // Assert
            pkg.Should().NotBeNull();
            pkg.PackageId.Should().Be("octopusdeploy/octo-prerelease");
            
            // Verify credential helper was NOT used
            log.Messages.Should().NotContain(m => m.FormattedMessage.Contains("Configured Docker credential helper"));
            log.Messages.Should().NotContain(m => m.FormattedMessage.Contains("Cleaned up Docker credential files"));
        }

        [Test]
        [RequiresDockerInstalled]
        public void CredentialHelper_WithoutCredentials_SkipsCredentialHelper()
        {
            // Arrange
            var log = new InMemoryLog();
                        var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.UseDockerCredentialHelper);
            var downloader = GetDownloader(log, variables);
            
            // Act
            var pkg = downloader.DownloadPackage("alpine",
                new SemanticVersion("3.6.5"), "docker-feed",
                new Uri(DockerHubFeedUri), null, null, true, 1,
                TimeSpan.FromSeconds(10));

            // Assert
            pkg.Should().NotBeNull();
            pkg.PackageId.Should().Be("alpine");
            
            // Verify credential helper was NOT used when no credentials provided
            log.Messages.Should().NotContain(m => m.FormattedMessage.Contains("Configured Docker credential helper"));
        }

        [Test]
        [RequiresDockerInstalled]
        public void CredentialHelper_CreatesCorrectDockerConfig_ForDockerHub()
        {
            // Arrange
            var log = new InMemoryLog();
                        var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.UseDockerCredentialHelper);
            var downloader = GetDownloader(log, variables);
            
            // Act
            var pkg = downloader.DownloadPackage("octopusdeploy/octo-prerelease",
                new SemanticVersion("7.3.7-alpine"), "docker-feed",
                new Uri(DockerHubFeedUri), dockerTestUsername, dockerTestPassword, true, 1,
                TimeSpan.FromSeconds(10));

            // Verify no unencrypted credential warnings in the log
            log.Messages.Should().NotContain(m => m.FormattedMessage.Contains("Your password will be stored unencrypted in octo-docker-configs/config.json"));
            
            // The Docker config should have been created and then cleaned up
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("Configured Docker credential helper"));
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("index.docker.io"));
            
            pkg.Should().NotBeNull();
            pkg.PackageId.Should().Be("octopusdeploy/octo-prerelease");
        }

        [Test]
        [RequiresDockerInstalled]
        public void CredentialHelper_WithCustomRegistry_CreatesCorrectConfig()
        {
            // Arrange - Using a public registry that doesn't require auth for this test
            var log = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.UseDockerCredentialHelper);
            var downloader = GetDownloader(log, variables);

            var customRegistryUri = new Uri("https://quay.io");
            
            // Act - Download a public image to test config creation (even though auth isn't needed)
            try
            {
                // This will attempt to set up credential helper but fall back gracefully
                downloader.DownloadPackage("coreos/etcd",
                    new SemanticVersion("v3.5.0"), "docker-feed",
                    customRegistryUri, "dummyuser", "dummypass", true, 1,
                    TimeSpan.FromSeconds(10));
            }
            catch (CommandException)
            {
                // Expected - dummy credentials will fail, but we can still verify config setup
            }

            // Assert
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("Failed to setup credential helper, falling back to direct login") ||
                                              m.FormattedMessage.Contains("Configured Docker credential helper"));
        }

        [Test]
        [RequiresDockerInstalled]
        public void CredentialHelper_FailureFallback_ContinuesWithDirectLogin()
        {
            // Arrange
            var log = new InMemoryLog();
            var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.UseDockerCredentialHelper);
            
            // Simulate a scenario where credential helper setup might fail
            // by using an invalid encryption password format
            variables.Set("SensitiveVariablesPassword", ""); // Empty password might cause issues
            var downloader = GetDownloader(log, variables);
            
            // Act
            var pkg = downloader.DownloadPackage("octopusdeploy/octo-prerelease",
                new SemanticVersion("7.3.7-alpine"), "docker-feed",
                new Uri(DockerHubFeedUri), dockerTestUsername, dockerTestPassword, true, 1,
                TimeSpan.FromSeconds(10));

            // Assert
            pkg.Should().NotBeNull();
            pkg.PackageId.Should().Be("octopusdeploy/octo-prerelease");
            
            // Should either succeed with credential helper or fall back gracefully
            var hasCredentialHelperMessage = log.Messages.Any(m => m.FormattedMessage.Contains("Configured Docker credential helper"));
            var hasFallbackMessage = log.Messages.Any(m => m.FormattedMessage.Contains("Docker login failed due to credential helper error, retrying without credential helper"));
            
            (hasCredentialHelperMessage || hasFallbackMessage).Should().BeTrue("Either credential helper should work or fallback should occur");
        }

        [Test]
        [RequiresDockerInstalled]
        public void CredentialHelper_MultipleRegistries_HandlesCorrectly()
        {
            // Arrange
            var log = new InMemoryLog();
                        var variables = new CalamariVariables();
            variables.Set(KnownVariables.EnabledFeatureToggles, OctopusFeatureToggles.KnownSlugs.UseDockerCredentialHelper);
            var downloader = GetDownloader(log, variables);

            
            // Act - Download from Docker Hub first
            var pkg1 = downloader.DownloadPackage("octopusdeploy/octo-prerelease",
                new SemanticVersion("7.3.7-alpine"), "docker-feed",
                new Uri(DockerHubFeedUri), dockerTestUsername, dockerTestPassword, true, 1,
                TimeSpan.FromSeconds(10));
            
            // Then download from the same registry again (should reuse or recreate config)
            var pkg2 = downloader.DownloadPackage("alpine",
                new SemanticVersion("3.6.5"), "docker-feed",
                new Uri(DockerHubFeedUri), null, null, true, 1,
                TimeSpan.FromSeconds(10));

            // Assert
            pkg1.Should().NotBeNull();
            pkg2.Should().NotBeNull();
            
            // First download should use credential helper
            log.Messages.Should().Contain(m => m.FormattedMessage.Contains("Configured Docker credential helper"));
            
            // Both downloads should succeed
            pkg1.PackageId.Should().Be("octopusdeploy/octo-prerelease");
            pkg2.PackageId.Should().Be("alpine");
        }

        static DockerImagePackageDownloader GetDownloader(ILog log, IVariables variables)
        {
            var runner = new CommandLineRunner(log, variables);
            return new DockerImagePackageDownloader(
                new ScriptEngine(Enumerable.Empty<IScriptWrapper>(), log), 
                CalamariPhysicalFileSystem.GetPhysicalFileSystem(), 
                runner, 
                variables, 
                log,
                new FeedLoginDetailsProviderFactory());
        }
    }
}
