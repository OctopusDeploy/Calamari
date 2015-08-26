using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
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
            var salt = CreateEncryptedSensitiveVariablesFile(sensitiveVariablesFileName, encryptionPassword, sensitiveVariables);

            var result = new CalamariVariableDictionary(insensitiveVariablesFileName, sensitiveVariablesFileName, encryptionPassword, salt);

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

            var result = new CalamariVariableDictionary(insensitiveVariablesFileName, sensitiveVariablesFileName, null, null);

            Assert.AreEqual("sensitiveVariableValue", result.Get("sensitiveVariableName"));
            Assert.AreEqual("insensitiveVariableValue", result.Get("insensitiveVariableName"));
        }

        string CreateEncryptedSensitiveVariablesFile(string fileName, string encryptionPassword, Dictionary<string, string> variables)
        {
            using (var algorithm = new AesCryptoServiceProvider
            {
                Key = CalamariVariableDictionary.GetEncryptionKey(encryptionPassword)
            })
            using (var encryptor = algorithm.CreateEncryptor())
            using (var encryptedTextStream = new MemoryStream())
            using (var jsonStream = new MemoryStream())
            using (var streamWriter = new StreamWriter(jsonStream, Encoding.UTF8))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {
                using (var cryptoStream = new CryptoStream(encryptedTextStream, encryptor, CryptoStreamMode.Write))
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(jsonWriter, variables);
                    jsonWriter.Flush();
                    streamWriter.Flush();

                    var jsonBytes = jsonStream.ToArray();
                    cryptoStream.Write(jsonBytes, 0, jsonBytes.Length);
                }

                var encryptedBytes = encryptedTextStream.ToArray();
                fileSystem.WriteAllBytes(fileName, encryptedBytes);

                return Convert.ToBase64String(algorithm.IV);
            }
        }
    }
}