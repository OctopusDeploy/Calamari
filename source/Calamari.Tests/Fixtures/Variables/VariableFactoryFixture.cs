using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Util;
using Calamari.Util;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Variables
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
            var result = new VariablesFactory(fileSystem).Create(options);

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

            var result = new VariablesFactory(fileSystem).Create(options);

            Assert.AreEqual("firstSensitiveVariableValue", result.Get("firstSensitiveVariableName"));
            Assert.AreEqual("firstInsensitiveVariableValue", result.Get("firstInsensitiveVariableName"));
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Cannot decrypt sensitive-variables. Check your password is correct.")]
        public void ThrowsCommandExceptionIfUnableToDecrypt()
        {
            options.InputVariables.SensitiveVariablesPassword = "FakePassword";
            CreateSensitiveVariableFile();
            new VariablesFactory(fileSystem).Create(options);
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Unable to parse sensitive-variables as valid JSON.")]
        public void ThrowsCommandExceptionIfUnableToParseAsJson()
        {
            options.InputVariables.SensitiveVariablesPassword = null;
            File.WriteAllText(firstSensitiveVariablesFileName, "I Am Not JSON");
            new VariablesFactory(fileSystem).Create(options);
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
            File.WriteAllBytes(firstSensitiveVariablesFileName, new AesEncryption(encryptionPassword, AesEncryption.VariablesFileKeySize).Encrypt(JsonConvert.SerializeObject(firstSensitiveVariablesSet)));

            var secondSensitiveVariablesSet = new Dictionary<string, string>
            {
                {"secondSensitiveVariableName", "secondSensitiveVariableValue"}
            };
            File.WriteAllBytes(secondSensitiveVariablesFileName, new AesEncryption(encryptionPassword, AesEncryption.VariablesFileKeySize).Encrypt(JsonConvert.SerializeObject(secondSensitiveVariablesSet)));
        }

        [Test]
        public void ShouldCheckVariableIsSet()
        {
            var variables = new VariablesFactory(fileSystem).Create(options);

            Assert.That(variables.IsSet("thisIsBogus"), Is.False);
            Assert.That(variables.IsSet("firstSensitiveVariableName"), Is.True);
        }


        [Test]
        public void VariablesInAdditionalVariablesPathAreContributed()
        {
            try
            {
                using (var varFile = new TemporaryFile(Path.GetTempFileName()))
                {
                    new CalamariVariables
                    {
                        {
                            "new.key", "new.value"
                        }
                    }.Save(varFile.FilePath);

                    Environment.SetEnvironmentVariable(VariablesFactory.AdditionalVariablesPathVariable, varFile.FilePath);

                    var variables = new VariablesFactory(CalamariPhysicalFileSystem.GetPhysicalFileSystem())
                        .Create(new CommonOptions("test"));

                    variables.Get("new.key").Should().Be("new.value");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(VariablesFactory.AdditionalVariablesPathVariable, null);
            }
        }

        [Test]
        public void IfAdditionalVariablesPathDoesNotExistAnExceptionIsThrown()
        {
            try
            {
                const string filePath = "c:/assuming/that/this/file/doesnt/exist.json";

                Environment.SetEnvironmentVariable(VariablesFactory.AdditionalVariablesPathVariable, filePath);

                new VariablesFactory(CalamariPhysicalFileSystem.GetPhysicalFileSystem())
                    .Invoking(c => c.Create(new CommonOptions("test")))
                    .Should()
                    .Throw<CommandException>()
                    // Make sure that the message says how to turn this feature off.
                    .Where(e => e.Message.Contains(VariablesFactory.AdditionalVariablesPathVariable))
                    // Make sure that the message says where it looked for the file.
                    .Where(e => e.Message.Contains(filePath));
            }
            finally
            {
                Environment.SetEnvironmentVariable(VariablesFactory.AdditionalVariablesPathVariable, null);
            }
        }
    }
}