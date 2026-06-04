using System.IO;
using Calamari.DockerCredentialHelper;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Docker
{
    [TestFixture]
    public class DockerCredentialProtocolFixture
    {
        const string Password = "password123";
        string configPath;
        DockerCredentialProtocol protocol;

        [SetUp]
        public void SetUp()
        {
            configPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(configPath);
            protocol = new DockerCredentialProtocol(new DockerCredentialStore());
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(configPath))
                Directory.Delete(configPath, true);
        }

        [Test]
        public void Store_ThenGet_WritesCredentialJsonToStdout()
        {
            var storeInput = new StringReader("{\"ServerURL\":\"https://example.com\",\"Username\":\"alice\",\"Secret\":\"s3cret\"}");
            var storeExit = protocol.Run("store", storeInput, new StringWriter(), new StringWriter(), Password, configPath);
            Assert.That(storeExit, Is.EqualTo(0));

            var getOutput = new StringWriter();
            var getExit = protocol.Run("get", new StringReader("https://example.com"), getOutput, new StringWriter(), Password, configPath);

            Assert.That(getExit, Is.EqualTo(0));
            Assert.That(getOutput.ToString(), Does.Contain("alice"));
            Assert.That(getOutput.ToString(), Does.Contain("s3cret"));
        }

        [Test]
        public void Get_WhenMissing_ReturnsExitOneAndNotFoundMessage()
        {
            var error = new StringWriter();
            var exit = protocol.Run("get", new StringReader("https://example.com"), new StringWriter(), error, Password, configPath);

            Assert.That(exit, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("credentials not found"));
        }

        [Test]
        public void Erase_RemovesCredential()
        {
            protocol.Run("store",
                         new StringReader("{\"ServerURL\":\"https://example.com\",\"Username\":\"alice\",\"Secret\":\"s3cret\"}"),
                         new StringWriter(), new StringWriter(), Password, configPath);

            var eraseExit = protocol.Run("erase", new StringReader("https://example.com"), new StringWriter(), new StringWriter(), Password, configPath);
            Assert.That(eraseExit, Is.EqualTo(0));

            var getExit = protocol.Run("get", new StringReader("https://example.com"), new StringWriter(), new StringWriter(), Password, configPath);
            Assert.That(getExit, Is.EqualTo(1));
        }

        [Test]
        public void Run_WithUnknownOperation_ReturnsExitOne()
        {
            var error = new StringWriter();
            var exit = protocol.Run("bogus", new StringReader(""), new StringWriter(), error, Password, configPath);

            Assert.That(exit, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("Invalid operation"));
        }

        [Test]
        public void Store_WithMalformedJson_ReturnsExitOneAndError()
        {
            var error = new StringWriter();
            var exit = protocol.Run("store", new StringReader("not json"), new StringWriter(), error, Password, configPath);

            Assert.That(exit, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Contain("Invalid store request"));
        }

        [Test]
        public void Store_WithEmptyInput_ReturnsExitOne()
        {
            var exit = protocol.Run("store", new StringReader(""), new StringWriter(), new StringWriter(), Password, configPath);
            Assert.That(exit, Is.EqualTo(1));
        }

        [Test]
        public void List_ReturnsEmptyJsonObjectAndExitZero()
        {
            var output = new StringWriter();
            var exit = protocol.Run("list", new StringReader(""), output, new StringWriter(), Password, configPath);

            Assert.That(exit, Is.EqualTo(0));
            Assert.That(output.ToString().Trim(), Is.EqualTo("{}"));
        }
    }
}
