using System;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.JsonVariables;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class JsonConfigurationVariablesConventionFixture
    {
        RunningDeployment deployment;
        IJsonConfigurationVariableReplacer configurationVariableReplacer;
        ICalamariFileSystem fileSystem;
        const string StagingDirectory = "C:\\applications\\Acme\\1.0.0";

        [SetUp]
        public void SetUp()
        {
            var variables = new CalamariVariables(); 
            variables.Set(KnownVariables.OriginalPackageDirectoryPath, TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0"));
            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("Packages"), variables);
            configurationVariableReplacer = Substitute.For<IJsonConfigurationVariableReplacer>();
            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);
        }

        [Test]
        public void ShouldNotRunIfVariableNotSet()
        {
            var convention = new JsonConfigurationVariablesConvention(configurationVariableReplacer, fileSystem);
            convention.Install(deployment);
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
            convention.Install(deployment);
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
            convention.Install(deployment);

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
            convention.Install(deployment);
            configurationVariableReplacer.DidNotReceiveWithAnyArgs().ModifyJsonFile(null, null);
        }
    }
}
