using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Integration.Packages.Download;
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
        const string TestEncryptionPassword = "TestPassword123!";
        const string TestServerUrl = "https://index.docker.io/v1/";
        const string TestUsername = "testuser";
        const string TestPassword = "testpass";
        readonly ICalamariFileSystem fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

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
        public void StoreCredentials_CreatesEncryptedCredentialFile()
        {
            // Act
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());
            credentialHelper.StoreCredentials(TestServerUrl, TestUsername, TestPassword, TestEncryptionPassword, dockerConfigPath);

            // Assert
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.Exists(credentialsDir).Should().BeTrue();
            
            var credentialFiles = Directory.GetFiles(credentialsDir, "*.cred");
            credentialFiles.Should().HaveCount(1);
            
            var credentialFile = credentialFiles[0];
            var encryptedBytes = File.ReadAllBytes(credentialFile);
            encryptedBytes.Should().NotBeEmpty();
        }

        [Test]
        public void GetCredentials_RetrievesStoredCredentials()
        {
            // Arrange
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());
            credentialHelper.StoreCredentials(TestServerUrl, TestUsername, TestPassword, TestEncryptionPassword, dockerConfigPath);

            // Act
            var retrievedCredential = credentialHelper.GetCredentials(TestServerUrl, TestEncryptionPassword, dockerConfigPath);

            // Assert
            retrievedCredential.Should().NotBeNull();
            retrievedCredential.Username.Should().Be(TestUsername);
            retrievedCredential.Secret.Should().Be(TestPassword);
        }

        [Test]
        public void GetCredentials_WithWrongPassword_ReturnsNull()
        {
            // Arrange
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());
            credentialHelper.StoreCredentials(TestServerUrl, TestUsername, TestPassword, TestEncryptionPassword, dockerConfigPath);

            // Act
            var retrievedCredential = credentialHelper.GetCredentials(TestServerUrl, "WrongPassword", dockerConfigPath);

            // Assert
            retrievedCredential.Should().BeNull();
        }

        [Test]
        public void GetCredentials_WithNonExistentCredentials_ReturnsNull()
        {
            // Act
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());
            var retrievedCredential = credentialHelper.GetCredentials("https://nonexistent.registry.com", TestEncryptionPassword, dockerConfigPath);

            // Assert
            retrievedCredential.Should().BeNull();
        }

        [Test]
        public void EraseCredentials_RemovesStoredCredentials()
        {
            // Arrange
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());
            credentialHelper.StoreCredentials(TestServerUrl, TestUsername, TestPassword, TestEncryptionPassword, dockerConfigPath);
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.GetFiles(credentialsDir, "*.cred").Should().HaveCount(1);

            // Act
            credentialHelper.EraseCredentials(TestServerUrl, dockerConfigPath);

            // Assert
            Directory.GetFiles(credentialsDir, "*.cred").Should().BeEmpty();
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
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());

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
        public void CleanupCredentials_RemovesCredentialsDirectory()
        {
            // Arrange
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());
            credentialHelper.StoreCredentials(TestServerUrl, TestUsername, TestPassword, TestEncryptionPassword, dockerConfigPath);
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.Exists(credentialsDir).Should().BeTrue();

            // Act
            credentialHelper.CleanupCredentials(dockerConfigPath);

            // Assert
            Directory.Exists(credentialsDir).Should().BeFalse();
        }

        [Test]
        public void StoreAndRetrieveMultipleCredentials_WorksCorrectly()
        {
            // Arrange
            var servers = new[]
            {
                ("https://index.docker.io/v1/", "dockeruser", "dockerpass"),
                ("https://myregistry.com", "myuser", "mypass"),
                ("https://another.registry.io", "anotheruser", "anotherpass")
            };
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());

            // Act - Store multiple credentials
            foreach (var (serverUrl, username, password) in servers)
            {
                credentialHelper.StoreCredentials(serverUrl, username, password, TestEncryptionPassword, dockerConfigPath);
            }

            // Assert - Retrieve and verify each credential
            foreach (var (serverUrl, expectedUsername, expectedPassword) in servers)
            {
                var credential = credentialHelper.GetCredentials(serverUrl, TestEncryptionPassword, dockerConfigPath);
                credential.Should().NotBeNull();
                credential.Username.Should().Be(expectedUsername);
                credential.Secret.Should().Be(expectedPassword);
            }

            // Verify separate files were created
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            Directory.GetFiles(credentialsDir, "*.cred").Should().HaveCount(3);
        }

        [Test]
        public void ServerUrlEncoding_HandlesSpecialCharacters()
        {
            // Arrange
            var serverUrls = new[]
            {
                "https://registry.with-dashes.com",
                "https://registry.with_underscores.com:8080",
                "https://registry/with/slashes",
                "https://registry.with.dots.and:8443/path"
            };
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());

            // Act & Assert
            foreach (var serverUrl in serverUrls)
            {
                credentialHelper.StoreCredentials(serverUrl, TestUsername, TestPassword, TestEncryptionPassword, dockerConfigPath);
                var credential = credentialHelper.GetCredentials(serverUrl, TestEncryptionPassword, dockerConfigPath);
                
                credential.Should().NotBeNull();
                credential.Username.Should().Be(TestUsername);
                credential.Secret.Should().Be(TestPassword);
            }
        }

        [Test]
        public void EncryptionIsSecure_PlaintextNotVisibleInFile()
        {
            // Arrange
            const string sensitivePassword = "SuperSecretPassword123!@#";
            var credentialHelper = new DockerCredentialHelper(fileSystem, Substitute.For<ILog>());
            
            // Act
            credentialHelper.StoreCredentials(TestServerUrl, TestUsername, sensitivePassword, TestEncryptionPassword, dockerConfigPath);

            // Assert
            var credentialsDir = Path.Combine(dockerConfigPath, "credentials");
            var credentialFiles = Directory.GetFiles(credentialsDir, "*.cred");
            var credentialFile = credentialFiles[0];
            var fileContent = File.ReadAllText(credentialFile);
            
            // Verify sensitive data is not visible in plaintext
            fileContent.Should().NotContain(TestUsername);
            fileContent.Should().NotContain(sensitivePassword);
            fileContent.Should().NotContain(TestServerUrl);
        }
        
        
        [Test]
        public void GetServerUrlForCredentialHelper_HandlesDockerHubCorrectly()
        {
            // Arrange & Act
            var dockerHubUri = new Uri("https://index.docker.io");
            
            var result = DockerCredentialHelper.GetServerUrlForCredentialHelper(dockerHubUri, "index.docker.io");
            
            // Assert
            result.Should().Be("https://index.docker.io/v1/");
        }

        [Test]
        public void GetServerUrlForCredentialHelper_HandlesCustomRegistryCorrectly()
        {
            // Arrange & Act
            var customUri = new Uri("https://myregistry.com:8080");
            
            var result = DockerCredentialHelper.GetServerUrlForCredentialHelper(customUri, "index.docker.io");
            
            // Assert
            result.Should().Be("https://myregistry.com:8080");
        }
    }
}
