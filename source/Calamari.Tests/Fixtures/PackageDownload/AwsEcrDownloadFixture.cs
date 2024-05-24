using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.ECR;
using Amazon.ECR.Model;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.Packages.Download;
using Calamari.Testing.Helpers;
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

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", Home);
        }

        [Test]
        public void HelmChartIsSuccessfullyDownloaded()
        {
            var regionEndpoint = RegionEndpoint.USEast1;
            const string repositoryName = "markc-test";
            const string imageTag = "0.1.0";

            var packagePhysicalFileMetadata = DoDownload(regionEndpoint, repositoryName, imageTag);

            packagePhysicalFileMetadata.Should().NotBeNull();

            using (new AssertionScope())
            {
                packagePhysicalFileMetadata.PackageId.Should().Be(repositoryName);
                packagePhysicalFileMetadata.Version.Should().Be(imageTag);
                packagePhysicalFileMetadata.Extension.Should().Be(".tgz");
                packagePhysicalFileMetadata.FullFilePath.Should().Contain(repositoryName);
            }
        }

        [Test]
        [RequiresDockerInstalled]
        public void DockerImageIsSuccessfullyDownloaded()
        {
            const string repositoryName = "markc-test";
            const string imageTag = "1.0.0";
            
            var regionEndpoint = RegionEndpoint.USEast1;    

            var packagePhysicalFileMetadata = DoDownload(regionEndpoint, repositoryName, imageTag);

            packagePhysicalFileMetadata.Should().NotBeNull();

            using (new AssertionScope())
            {
                packagePhysicalFileMetadata.PackageId.Should().Be(repositoryName);
                packagePhysicalFileMetadata.Version.ToString().Should().Be(imageTag);
                packagePhysicalFileMetadata.FullFilePath.Should().BeEmpty();
            }
        }

        static PackagePhysicalFileMetadata DoDownload(RegionEndpoint regionEndpoint, string repositoryName, string imageTag)
        {
            var log = Substitute.For<ILog>();
            var runner = new CommandLineRunner(log, new CalamariVariables());
            var engine = new ScriptEngine(Enumerable.Empty<IScriptWrapper>(), log);
            var strategy = new PackageDownloaderStrategy(log,
                                                         engine,
                                                         CalamariPhysicalFileSystem.GetPhysicalFileSystem(),
                                                         runner,
                                                         new CalamariVariables());

            var authDetails = GetEcrAuthDetails(regionEndpoint);
            var registryUri = new Uri(authDetails.Registry);

            var packagePhysicalFileMetadata = strategy.DownloadPackage(repositoryName,
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

        static (string Username, string Password, string Registry) GetEcrAuthDetails(RegionEndpoint regionEndpoint)
        {
            var ecrClient = new AmazonECRClient(regionEndpoint);
            var authTokenRequest = new GetAuthorizationTokenRequest();
            var authTokenResponse = ecrClient.GetAuthorizationTokenAsync(authTokenRequest).Result;

            var authorizationData = authTokenResponse.AuthorizationData[0];
            var token = authorizationData.AuthorizationToken;
            var decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var usernamePassword = decodedToken.Split(':');
            var username = usernamePassword[0];
            var password = usernamePassword[1];
            var registry = authorizationData.ProxyEndpoint;

            return (username, password, registry);
        }
    }
}