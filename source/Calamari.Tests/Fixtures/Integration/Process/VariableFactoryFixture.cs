using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Tests.Fixtures.Util;
using Calamari.Util;
using Calamari.Variables;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Integration.Process
{
    [TestFixture]
    public class VariableFactoryFixture
    {
        string tempDirectory;
        string firstInsensitiveVariablesFileName;
        string firstSensitiveVariablesFileName;
        string secondSensitiveVariablesFileName;
        ICalamariFileSystem fileSystem;
        CommonOptions options;
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

            options = CommonOptions.Parse(new[]
            {
                "Test",
                "--variables", 
                firstInsensitiveVariablesFileName,
                "--sensitiveVariables",
                firstSensitiveVariablesFileName,
                "--sensitiveVariables",
                secondSensitiveVariablesFileName,
                "--sensitiveVariablesPassword",
                encryptionPassword
            });
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
            var result = VariablesFactory.Create(fileSystem, options);

            Assert.AreEqual("firstSensitiveVariableValue", result.Get("firstSensitiveVariableName"));
            Assert.AreEqual("secondSensitiveVariableValue", result.Get("secondSensitiveVariableName"));
            Assert.AreEqual("firstInsensitiveVariableValue", result.Get("firstInsensitiveVariableName"));
        }

        [Test]
        public void ShouldIncludeCleartextSensitiveVariables()
        {
            options.InputVariables.SensitiveVariablesPassword = null;
            
            var sensitiveVariables = new Dictionary<string, string> { { "firstSensitiveVariableName", "firstSensitiveVariableValue"} };
            File.WriteAllText(firstSensitiveVariablesFileName, JsonConvert.SerializeObject(sensitiveVariables));
            File.WriteAllText(secondSensitiveVariablesFileName, "{}");
            
            var result = VariablesFactory.Create(fileSystem, options);

            Assert.AreEqual("firstSensitiveVariableValue", result.Get("firstSensitiveVariableName"));
            Assert.AreEqual("firstInsensitiveVariableValue", result.Get("firstInsensitiveVariableName"));
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Cannot decrypt sensitive-variables. Check your password is correct.")]
        public void ThrowsCommandExceptionIfUnableToDecrypt()
        {
            options.InputVariables.SensitiveVariablesPassword = "FakePassword";
            CreateSensitiveVariableFile();
            VariablesFactory.Create(fileSystem, options);
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Unable to parse sensitive-variables as valid JSON.")]
        public void ThrowsCommandExceptionIfUnableToParseAsJson()
        {
            options.InputVariables.SensitiveVariablesPassword = null;
            File.WriteAllText(firstSensitiveVariablesFileName, "I Am Not JSON");
            VariablesFactory.Create(fileSystem, options);
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
            var variables = VariablesFactory.Create(fileSystem, options);

            Assert.That(variables.IsSet("thisIsBogus"), Is.False);
            Assert.That(variables.IsSet("firstSensitiveVariableName"), Is.True);
        }
    }
}