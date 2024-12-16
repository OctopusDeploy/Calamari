using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.Download;
using Calamari.Testing;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.PackageDownload
{
    [TestFixture]
    public class AwsEcrDownloadFixture : CalamariFixture
    {
        static readonly string Home = Path.GetTempPath();
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", Home);
        }

        [Test]
        public async Task HelmChartIsSuccessfullyDownloaded()
        {
            var regionEndpoint = RegionEndpoint.USWest2;
            const string repositoryName = "calamari-testing-helm-chart";
            const string imageTag = "0.1.0";

            var packagePhysicalFileMetadata = await DoDownload(regionEndpoint, repositoryName, imageTag, cancellationTokenSource.Token);

            packagePhysicalFileMetadata.Should().NotBeNull();

            using (new AssertionScope())
            {
                packagePhysicalFileMetadata.PackageId.Should().Be(repositoryName);
                packagePhysicalFileMetadata.Version.Should().Be(new SemanticVersion(imageTag));
                packagePhysicalFileMetadata.Extension.Should().Be(".tgz");
                packagePhysicalFileMetadata.FullFilePath.Should().Contain(repositoryName);
            }
        }

        [Test]
        [RequiresDockerInstalled]
        public async Task DockerImageIsSuccessfullyDownloaded()
        {
            const string repositoryName = "calamari-testing-container-image";
            const string imageTag = "1.0.0";

            var regionEndpoint = RegionEndpoint.USWest2;

            var packagePhysicalFileMetadata = await DoDownload(regionEndpoint, repositoryName, imageTag, cancellationTokenSource.Token);

            packagePhysicalFileMetadata.Should().NotBeNull();

            using (new AssertionScope())
            {
                packagePhysicalFileMetadata.PackageId.Should().Be(repositoryName);
                packagePhysicalFileMetadata.Version.ToString().Should().Be(imageTag);
                packagePhysicalFileMetadata.FullFilePath.Should().BeEmpty();
            }
        }

        static async Task<PackagePhysicalFileMetadata> DoDownload(RegionEndpoint regionEndpoint, string repositoryName, string imageTag, CancellationToken cancellationToken)
        {
            var log = Substitute.For<ILog>();
            var runner = new CommandLineRunner(log, new CalamariVariables());
            var engine = new ScriptEngine(Enumerable.Empty<IScriptWrapper>(), log);
            var strategy = new PackageDownloaderStrategy(
                log,
                engine,
                CalamariPhysicalFileSystem.GetPhysicalFileSystem(),
                runner,
                new CalamariVariables());

            var authDetails = await GetEcrAuthDetails(regionEndpoint, cancellationToken);
            var registryUri = new Uri(authDetails.RegistryUri);

            var packagePhysicalFileMetadata = strategy.DownloadPackage(
                repositoryName,
                SemVerFactory.CreateVersion(imageTag),
                "",
                registryUri,
                FeedType.AwsElasticContainerRegistry,
                authDetails.Username,
                authDetails.Password,
                true,
                1,
                TimeSpan.FromSeconds(30));
            return packagePhysicalFileMetadata;
        }

        static async Task<AwsElasticContainerRegistryCredentials.TemporaryCredentials> GetEcrAuthDetails(RegionEndpoint regionEndpoint, CancellationToken cancellationToken)
        {
            return new AwsElasticContainerRegistryCredentials().RetrieveTemporaryCredentials(
                await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3AccessKey, cancellationToken),
                await ExternalVariables.Get(ExternalVariable.AwsCloudFormationAndS3SecretKey, cancellationToken),
                regionEndpoint.SystemName);
        }
    }
}