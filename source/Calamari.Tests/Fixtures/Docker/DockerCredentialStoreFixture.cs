using System.IO;
using Calamari.Common.Features.Docker;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Docker
{
    [TestFixture]
    public class DockerCredentialStoreFixture
    {
        string configPath;

        [SetUp]
        public void SetUp()
        {
            configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(configPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(configPath))
                Directory.Delete(configPath, true);
        }

        [Test]
        public void StoreThenGet_RoundTripsCredentials()
        {
            var store = new DockerCredentialStore();
            store.Store("https://index.docker.io/v1/", "alice", "s3cret", "password123", configPath);

            var result = store.Get("https://index.docker.io/v1/", "password123", configPath);

            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Username, Is.EqualTo("alice"));
            Assert.That(result.Secret, Is.EqualTo("s3cret"));
        }

        [Test]
        public void Get_WithWrongPassword_ReturnsNull()
        {
            var store = new DockerCredentialStore();
            store.Store("https://example.com", "bob", "pw", "password123", configPath);

            Assert.That(store.Get("https://example.com", "WrongPassword", configPath), Is.Null);
        }

        [Test]
        public void Get_WhenNoCredentialStored_ReturnsNull()
        {
            var store = new DockerCredentialStore();
            Assert.That(store.Get("https://example.com", "password123", configPath), Is.Null);
        }

        [Test]
        public void Erase_RemovesStoredCredential()
        {
            var store = new DockerCredentialStore();
            store.Store("https://example.com", "bob", "pw", "password123", configPath);

            store.Erase("https://example.com", configPath);

            Assert.That(store.Get("https://example.com", "password123", configPath), Is.Null);
        }

        [Test]
        public void Get_WithCorruptCredentialFile_ReturnsNull()
        {
            var serverUrl = "https://example.com";
            var credentialsDir = Path.Combine(configPath, "credentials");
            Directory.CreateDirectory(credentialsDir);
            File.WriteAllBytes(Path.Combine(credentialsDir, DockerCredentialStore.GetCredentialFileName(serverUrl)), new byte[] { 1, 2, 3, 4, 5 });

            var store = new DockerCredentialStore();
            Assert.That(store.Get(serverUrl, "password123", configPath), Is.Null);
        }
    }
}
