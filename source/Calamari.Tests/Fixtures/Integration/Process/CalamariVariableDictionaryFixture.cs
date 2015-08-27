using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
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

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); 
            insensitiveVariablesFileName = Path.Combine(tempDirectory, "myVariables.json");
            sensitiveVariablesFileName = Path.ChangeExtension(insensitiveVariablesFileName, "secret");
            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
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
            const string encryptionPassword = "HumptyDumpty!";

            var insensitiveVariables = new VariableDictionary(insensitiveVariablesFileName);
            insensitiveVariables.Set("insensitiveVariableName", "insensitiveVariableValue");
            insensitiveVariables.Save();

            var sensitiveVariables = new Dictionary<string, string>
            {
                {"sensitiveVariableName", "sensitiveVariableValue"}
            };
            File.WriteAllBytes(sensitiveVariablesFileName, new AesEncryption(encryptionPassword).Encrypt(JsonConvert.SerializeObject(sensitiveVariables)));
            var result = new CalamariVariableDictionary(insensitiveVariablesFileName, sensitiveVariablesFileName, encryptionPassword);

            Assert.AreEqual("sensitiveVariableValue", result.Get("sensitiveVariableName"));
            Assert.AreEqual("insensitiveVariableValue", result.Get("insensitiveVariableName"));
        }

        [Test]
        public void ShouldIncludeCleartextSensitiveVariables()
        {
            var insensitiveVariables = new VariableDictionary(insensitiveVariablesFileName);
            insensitiveVariables.Set("insensitiveVariableName", "insensitiveVariableValue");
            insensitiveVariables.Save();

            var sensitiveVariables = new Dictionary<string, string>
            {
                {"sensitiveVariableName", "sensitiveVariableValue"}
            };

            File.WriteAllText(sensitiveVariablesFileName, JsonConvert.SerializeObject(sensitiveVariables));
            var result = new CalamariVariableDictionary(insensitiveVariablesFileName, sensitiveVariablesFileName, null);

            Assert.AreEqual("sensitiveVariableValue", result.Get("sensitiveVariableName"));
            Assert.AreEqual("insensitiveVariableValue", result.Get("insensitiveVariableName"));
        }
    }
}