using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Tests.Fixtures.Util;
using Calamari.Util;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Integration.Process
{
    [TestFixture]
    public class CalamariVariableDictionaryFixture
    {
        string tempDirectory;
        string firstInsensitiveVariablesFileName;
        string firstSensitiveVariablesFileName;
        string secondSensitiveVariablesFileName;
        ICalamariFileSystem fileSystem;
        const string encryptionPassword = "HumptyDumpty!";

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            firstSensitiveVariablesFileName = Path.Combine(tempDirectory, "firstVariableSet.secret");
            secondSensitiveVariablesFileName = Path.Combine(tempDirectory, "secondVariableSet.secret");
            firstInsensitiveVariablesFileName = Path.ChangeExtension(firstSensitiveVariablesFileName, "json");
            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            fileSystem.EnsureDirectoryExists(tempDirectory);

            CreateSensitiveVariableFile();
            CreateInSensitiveVariableFile();
        }

        [TearDown]
        public void CleanUp()
        {
            if (fileSystem.DirectoryExists(tempDirectory))
                fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void ShouldIncludeEncryptedSensitiveVariables()
        {
            var result = new CalamariVariableDictionary(firstInsensitiveVariablesFileName, new List<string>() { firstSensitiveVariablesFileName, secondSensitiveVariablesFileName }, encryptionPassword);

            Assert.AreEqual("firstSensitiveVariableValue", result.Get("firstSensitiveVariableName"));
            Assert.AreEqual("secondSensitiveVariableValue", result.Get("secondSensitiveVariableName"));
            Assert.AreEqual("firstInsensitiveVariableValue", result.Get("firstInsensitiveVariableName"));
        }

        [Test]
        public void ShouldIncludeCleartextSensitiveVariables()
        {
            var sensitiveVariables = new Dictionary<string, string> { { "firstSensitiveVariableName", "firstSensitiveVariableValue"} };
            File.WriteAllText(firstSensitiveVariablesFileName, JsonConvert.SerializeObject(sensitiveVariables));

            var result = new CalamariVariableDictionary(firstInsensitiveVariablesFileName, new List<string>() { firstSensitiveVariablesFileName }, null);

            Assert.AreEqual("firstSensitiveVariableValue", result.Get("firstSensitiveVariableName"));
            Assert.AreEqual("firstInsensitiveVariableValue", result.Get("firstInsensitiveVariableName"));
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Cannot decrypt sensitive-variables. Check your password is correct.")]
        public void ThrowsCommandExceptionIfUnableToDecrypt()
        {
            new CalamariVariableDictionary(firstInsensitiveVariablesFileName, new List<string>() { firstSensitiveVariablesFileName }, "FakePassword");
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Unable to parse sensitive-variables as valid JSON.")]
        public void ThrowsCommandExceptionIfUnableToParseAsJson()
        {
            File.WriteAllText(firstSensitiveVariablesFileName, "I Am Not JSON");
            new CalamariVariableDictionary(firstInsensitiveVariablesFileName, new List<string>() { firstSensitiveVariablesFileName }, null);
        }

        void CreateInSensitiveVariableFile()
        {
            var firstInsensitiveVariableSet = new VariableDictionary(firstInsensitiveVariablesFileName);
            firstInsensitiveVariableSet.Set("firstInsensitiveVariableName", "firstInsensitiveVariableValue");
            firstInsensitiveVariableSet.Save();
        }

        void CreateSensitiveVariableFile()
        {
            var firstSensitiveVariablesSet = new Dictionary<string, string>
            {
                {"firstSensitiveVariableName", "firstSensitiveVariableValue"}
            };
            File.WriteAllBytes(firstSensitiveVariablesFileName, new AesEncryption(encryptionPassword).Encrypt(JsonConvert.SerializeObject(firstSensitiveVariablesSet)));

            var secondSensitiveVariablesSet = new Dictionary<string, string>
            {
                {"secondSensitiveVariableName", "secondSensitiveVariableValue"}
            };
            File.WriteAllBytes(secondSensitiveVariablesFileName, new AesEncryption(encryptionPassword).Encrypt(JsonConvert.SerializeObject(secondSensitiveVariablesSet)));
        }

        [Test]
        public void ShouldCheckVariableIsSet()
        {
            var variables = new CalamariVariableDictionary(firstInsensitiveVariablesFileName, new List<string>(){ firstSensitiveVariablesFileName }, encryptionPassword);

            Assert.That(variables.IsSet("thisIsBogus"), Is.False);
            Assert.That(variables.IsSet("firstSensitiveVariableName"), Is.True);
        }
    }
}