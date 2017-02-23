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
        string insensitiveVariablesFileName;
        string sensitiveVariablesFileName;
        ICalamariFileSystem fileSystem;
        const string encryptionPassword = "HumptyDumpty!";

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); 
            insensitiveVariablesFileName = Path.Combine(tempDirectory, "myVariables.json");
            sensitiveVariablesFileName = Path.ChangeExtension(insensitiveVariablesFileName, "secret");
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
            var result = new CalamariVariableDictionary(insensitiveVariablesFileName, sensitiveVariablesFileName, encryptionPassword);

            Assert.AreEqual("sensitiveVariableValue", result.Get("sensitiveVariableName"));
            Assert.AreEqual("insensitiveVariableValue", result.Get("insensitiveVariableName"));
        }

        [Test]
        public void ShouldIncludeCleartextSensitiveVariables()
        {
            var sensitiveVariables = new Dictionary<string, string> { {"sensitiveVariableName", "sensitiveVariableValue"} };
            File.WriteAllText(sensitiveVariablesFileName, JsonConvert.SerializeObject(sensitiveVariables));

            var result = new CalamariVariableDictionary(insensitiveVariablesFileName, sensitiveVariablesFileName, null);

            Assert.AreEqual("sensitiveVariableValue", result.Get("sensitiveVariableName"));
            Assert.AreEqual("insensitiveVariableValue", result.Get("insensitiveVariableName"));
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Cannot decrypt sensitive-variables. Check your password is correct.")]
        public void ThrowsCommandExceptionIfUnableToDecrypt()
        {
            new CalamariVariableDictionary(insensitiveVariablesFileName, sensitiveVariablesFileName, "FakePassword");
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Unable to parse sensitive-variables as valid JSON.")]
        public void ThrowsCommandExceptionIfUnableToParseAsJson()
        {
            File.WriteAllText(sensitiveVariablesFileName, "I Am Not JSON");
            new CalamariVariableDictionary(insensitiveVariablesFileName, sensitiveVariablesFileName, null);
        }

        private void CreateInSensitiveVariableFile()
        {
            var insensitiveVariables = new VariableDictionary(insensitiveVariablesFileName);
            insensitiveVariables.Set("insensitiveVariableName", "insensitiveVariableValue");
            insensitiveVariables.Save();
        }

        private void CreateSensitiveVariableFile()
        {
            var sensitiveVariables = new Dictionary<string, string>
            {
                {"sensitiveVariableName", "sensitiveVariableValue"}
            };
            File.WriteAllBytes(sensitiveVariablesFileName, new AesEncryption(encryptionPassword).Encrypt(JsonConvert.SerializeObject(sensitiveVariables)));
        }

        [Test]
        public void ShouldCheckVariableIsSet()
        {
            var variables = new CalamariVariableDictionary(insensitiveVariablesFileName, sensitiveVariablesFileName, encryptionPassword);

            Assert.That(variables.IsSet("thisIsBogus"), Is.False);
            Assert.That(variables.IsSet("sensitiveVariableName"), Is.True);
        }
    }
}