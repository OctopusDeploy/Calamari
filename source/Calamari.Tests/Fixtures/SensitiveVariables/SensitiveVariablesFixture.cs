using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Calamari.Integration.FileSystem;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.SensitiveVariables
{
    [TestFixture]
    public class SensitiveVariablesFixture
    {
        string insensitiveVariablesFileName;
        string sensitiveVariablesFileName;
        ICalamariFileSystem fileSystem;
        Calamari.Deployment.SensitiveVariables subject;

        [SetUp]
        public void SetUp()
        {
            var tempDirectory = Path.GetTempPath();
            insensitiveVariablesFileName = Path.Combine(tempDirectory, "myVariables.json");
            sensitiveVariablesFileName = Path.ChangeExtension(insensitiveVariablesFileName, "secret");
            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            subject = new Calamari.Deployment.SensitiveVariables(fileSystem);
        }

        [TearDown]
        public void CleanUp()
        {
           if (File.Exists(insensitiveVariablesFileName)) 
               File.Delete(insensitiveVariablesFileName);

            if (File.Exists(sensitiveVariablesFileName))
                File.Delete(sensitiveVariablesFileName);
        }

        [Test]
        public void ShouldIncludeSensitiveVariables()
        {
            const string encryptionPassword = "HumptyDumpty!";

            var insensitiveVariables = new VariableDictionary(insensitiveVariablesFileName);
            insensitiveVariables.Set("insensitiveVariableName", "insensitiveVariableValue");
            insensitiveVariables.Save();

            var sensitiveVariables = new Dictionary<string, string>();
            sensitiveVariables.Add("sensitiveVariableName", "sensitiveVariableValue");
            var salt = CreateEncryptedSensitiveVariablesFile(sensitiveVariablesFileName, encryptionPassword, sensitiveVariables);

            var result = subject.IncludeSensitiveVariables(insensitiveVariablesFileName, encryptionPassword, salt);

            Assert.AreEqual("sensitiveVariableValue", result.Get("sensitiveVariableName"));

        }

        static string CreateEncryptedSensitiveVariablesFile(string fileName, string encryptionPassword, Dictionary<string, string> variables)
        {
            using (var algorithm = new AesCryptoServiceProvider{ Key = Calamari.Deployment.SensitiveVariables.GetEncryptionKey(encryptionPassword)})
            using (var encryptor = algorithm.CreateEncryptor())
            using (var fileStream = new FileStream(fileName, FileMode.Create))
            using (var encryptedTextStream = new MemoryStream())
            using (var cryptoStream = new CryptoStream(encryptedTextStream, encryptor, CryptoStreamMode.Write))
            using (var jsonStream = new MemoryStream())
            using (var streamWriter = new StreamWriter(jsonStream, Encoding.UTF8))
            using (var jsonWriter = new JsonTextWriter(streamWriter))
            {

                var serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, variables);
                jsonWriter.Flush();
                streamWriter.Flush();

                var jsonBytes = jsonStream.ToArray(); 
                var json = Encoding.UTF8.GetString(jsonBytes);

                cryptoStream.Write(jsonBytes, 0, jsonBytes.Length);
                cryptoStream.Flush();

                var bytes =  Encoding.UTF8.GetBytes(Convert.ToBase64String(encryptedTextStream.ToArray()));
                fileStream.Write(bytes, 0, bytes.Length);
                fileStream.Flush();

                return Convert.ToBase64String(algorithm.IV);
            }
        }
    }
}