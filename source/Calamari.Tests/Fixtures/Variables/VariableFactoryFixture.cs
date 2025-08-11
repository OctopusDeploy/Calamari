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
using Calamari.Tests.Helpers;
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
        string firstVariableFileName;
        string secondVariableFileName;
        ICalamariFileSystem fileSystem;
        CommonOptions options;
        const string encryptionPassword = "HumptyDumpty!";

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            firstVariableFileName = Path.Combine(tempDirectory, "firstVariableSet.secret");
            secondVariableFileName = Path.Combine(tempDirectory, "secondVariableSet.secret");
            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            fileSystem.EnsureDirectoryExists(tempDirectory);

            CreateEncryptedVariableFile();

            options = CommonOptions.Parse(new[]
            {
                "Test",
                "--variables",
                firstVariableFileName,
                "--variables",
                secondVariableFileName,
                "--variablesPassword",
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
        public void ShouldIncludeEncryptedVariables()
        {
            var result = new VariablesFactory(fileSystem, new SilentLog()).Create(options);

            Assert.AreEqual("firstVariableValue", result.Get("firstVariableName"));
            Assert.AreEqual("secondVariableValue", result.Get("secondVariableName"));
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Cannot decrypt variables. Check your password is correct.")]
        public void ThrowsCommandExceptionIfUnableToDecrypt()
        {
            options.InputVariables.VariablesPassword = "FakePassword";
            CreateEncryptedVariableFile();
            new VariablesFactory(fileSystem, new SilentLog()).Create(options);
        }

        [Test]
        [ExpectedException(typeof(CommandException), ExpectedMessage = "Unable to parse variables as valid JSON.")]
        public void ThrowsCommandExceptionIfUnableToParseAsJson()
        {
            options.InputVariables.VariablesPassword = null;
            File.WriteAllText(firstVariableFileName, "I Am Not JSON");
            new VariablesFactory(fileSystem, new SilentLog()).Create(options);
        }

        void CreateEncryptedVariableFile()
        {
            var firstVariableCollection = new CalamariExecutionVariableCollection
            {
                new CalamariExecutionVariable("firstVariableName", "firstVariableValue", false)
            };
            File.WriteAllBytes(firstVariableFileName, AesEncryption.ForServerVariables(encryptionPassword).Encrypt(firstVariableCollection.ToJsonString()));
            
            var secondVariableCollection = new CalamariExecutionVariableCollection
            {
                new CalamariExecutionVariable("secondVariableName", "secondVariableValue", true)
            };
            File.WriteAllBytes(secondVariableFileName, AesEncryption.ForServerVariables(encryptionPassword).Encrypt(secondVariableCollection.ToJsonString()));
        }

        [Test]
        public void ShouldCheckVariableIsSet()
        {
            var variables = new VariablesFactory(fileSystem, new SilentLog()).Create(options);

            Assert.That(variables.IsSet("thisIsBogus"), Is.False);
            Assert.That(variables.IsSet("firstVariableName"), Is.True);
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

                    var variables = new VariablesFactory(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new SilentLog())
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

                new VariablesFactory(CalamariPhysicalFileSystem.GetPhysicalFileSystem(), new SilentLog())
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