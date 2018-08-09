using System;
using System.Linq;
using Calamari.Commands;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Processes;
using Calamari.Shared;
using Calamari.Shared.Commands;
using Calamari.Shared.FileSystem;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class JsonConfigurationVariablesConventionFixture
    {
        IExecutionContext deployment;
        IJsonConfigurationVariableReplacer configurationVariableReplacer;
        ICalamariFileSystem fileSystem;
        const string StagingDirectory = "C:\\applications\\Acme\\1.0.0";

        [SetUp]
        public void SetUp()
        {
            var variables = new CalamariVariableDictionary(); 
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0"));
            deployment = new CalamariExecutionContext(TestEnvironment.ConstructRootedPath("Packages"), variables);
            configurationVariableReplacer = Substitute.For<IJsonConfigurationVariableReplacer>();
            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);
        }

        [Test]
        public void ShouldNotRunIfVariableNotSet()
        {
            var convention = new JsonConfigurationVariablesConvention(configurationVariableReplacer, fileSystem);
            convention.Run(deployment);
            configurationVariableReplacer.DidNotReceiveWithAnyArgs().ModifyJsonFile(null, null);
        }

        [Test]
        public void ShouldFindAndCallModifyOnTargetFile()
        {
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), "appsettings.environment.json")
                .Returns(new[] {TestEnvironment.ConstructRootedPath("applications" ,"Acme", "1.0.0", "appsettings.environment.json")});

            deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesEnabled, "true");
            deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesTargets, "appsettings.environment.json");
            var convention = new JsonConfigurationVariablesConvention(configurationVariableReplacer, fileSystem);
            convention.Run(deployment);
            configurationVariableReplacer.Received().ModifyJsonFile(TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", "appsettings.environment.json"), deployment.Variables);
        }

        [Test]
        public void ShouldFindAndCallModifyOnAllTargetFiles()
        {
            var targetFiles = new[]
            {
                "config.json",
                "config.dev.json",
                "config.prod.json"
            };

            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), "config.json")
                .Returns(new[] {targetFiles[0]}.Select(t => TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", t)));
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), "config.*.json")
                .Returns(targetFiles.Skip(1).Select(t => TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", t)));

            deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesEnabled, "true");
            deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesTargets, string.Join(Environment.NewLine, "config.json", "config.*.json"));

            var convention = new JsonConfigurationVariablesConvention(configurationVariableReplacer, fileSystem);
            convention.Run(deployment);

            foreach (var targetFile in targetFiles)
            {
                configurationVariableReplacer.Received()
                    .ModifyJsonFile(TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", targetFile), deployment.Variables);
            }
        }

        [Test]
        public void ShouldNotAttemptToRunOnDirectories()
        {
            deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesEnabled, "true");
            deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesTargets, "approot");
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);

            var convention = new JsonConfigurationVariablesConvention(configurationVariableReplacer, fileSystem);
            convention.Run(deployment);
            configurationVariableReplacer.DidNotReceiveWithAnyArgs().ModifyJsonFile(null, null);
        }
    }
}
