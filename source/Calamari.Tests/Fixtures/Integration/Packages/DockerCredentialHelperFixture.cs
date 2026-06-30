using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class DockerCredentialHelperFixture
    {
        string tempDirectory;
        string dockerConfigPath;

        [SetUp]
        public void Setup()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            dockerConfigPath = Path.Combine(tempDirectory, "docker-config");
            Directory.CreateDirectory(dockerConfigPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Test]
        public void CreateDockerConfig_CreatesValidConfigFile()
        {
            // Arrange
            var credHelpers = new Dictionary<string, string>
            {
                ["index.docker.io"] = "octopus",
                ["docker.io"] = "octopus",
                ["myregistry.com"] = "octopus"
            };
            var credentialHelper = new Calamari.Integration.Packages.Download.DockerCredentialHelper(Substitute.For<ILog>());

            // Act
            var configPath = credentialHelper.CreateDockerConfig(dockerConfigPath, credHelpers);

            // Assert
            File.Exists(configPath).Should().BeTrue();
            configPath.Should().Be(Path.Combine(dockerConfigPath, "config.json"));

            var configContent = File.ReadAllText(configPath);
            configContent.Should().Contain("\"credHelpers\"");
            configContent.Should().Contain("\"index.docker.io\": \"octopus\"");
            configContent.Should().Contain("\"docker.io\": \"octopus\"");
            configContent.Should().Contain("\"myregistry.com\": \"octopus\"");
        }

        [Test]
        public void GetServerUrlForCredentialHelper_HandlesDockerHubCorrectly()
        {
            var dockerHubUri = new Uri("https://index.docker.io");

            var result = Calamari.Integration.Packages.Download.DockerCredentialHelper.GetServerUrlForCredentialHelper(dockerHubUri, "index.docker.io");

            result.Should().Be("https://index.docker.io/v1/");
        }

        [Test]
        public void GetServerUrlForCredentialHelper_HandlesCustomRegistryCorrectly()
        {
            var customUri = new Uri("https://myregistry.com:8080");

            var result = Calamari.Integration.Packages.Download.DockerCredentialHelper.GetServerUrlForCredentialHelper(customUri, "index.docker.io");

            result.Should().Be("https://myregistry.com:8080");
        }
    }
}
