using System;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class JsonConfigurationVariablesConventionFixture
    {
        RunningDeployment deployment;
        IStructuredConfigVariableReplacer configVariableReplacer;
        ICalamariFileSystem fileSystem;
        const string StagingDirectory = "C:\\applications\\Acme\\1.0.0";

        [SetUp]
        public void SetUp()
        {
            var variables = new CalamariVariables(); 
            variables.Set(KnownVariables.OriginalPackageDirectoryPath, TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0"));
            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("Packages"), variables);
            configVariableReplacer = Substitute.For<IStructuredConfigVariableReplacer>();
            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);
        }

        [Test]
        public void ShouldNotRunIfVariableNotSet()
        {
            var service = new StructuredConfigVariablesService(configVariableReplacer, fileSystem);
            var convention = new JsonConfigurationVariablesConvention(service);
            convention.Install(deployment);
            configVariableReplacer.DidNotReceiveWithAnyArgs().ModifyFile(null, null);
        }

        [Test]
        public void ShouldFindAndCallModifyOnTargetFile()
        {
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), "appsettings.environment.json")
                .Returns(new[] {TestEnvironment.ConstructRootedPath("applications" ,"Acme", "1.0.0", "appsettings.environment.json")});

            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesEnabled, "true");
            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, "appsettings.environment.json");
            
            var service = new StructuredConfigVariablesService(configVariableReplacer, fileSystem);
            var convention = new JsonConfigurationVariablesConvention(service);
            convention.Install(deployment);
            configVariableReplacer.Received().ModifyFile(TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", "appsettings.environment.json"), deployment.Variables);
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

            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesEnabled, "true");
            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, string.Join(Environment.NewLine, "config.json", "config.*.json"));

            var service = new StructuredConfigVariablesService(configVariableReplacer, fileSystem);
            var convention = new JsonConfigurationVariablesConvention(service);
            convention.Install(deployment);

            foreach (var targetFile in targetFiles)
            {
                configVariableReplacer.Received()
                    .ModifyFile(TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", targetFile), deployment.Variables);
            }
        }

        [Test]
        public void ShouldNotAttemptToRunOnDirectories()
        {
            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesEnabled, "true");
            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, "approot");
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);

            var service = new StructuredConfigVariablesService(configVariableReplacer, fileSystem);
            var convention = new JsonConfigurationVariablesConvention(service);
            convention.Install(deployment);
            configVariableReplacer.DidNotReceiveWithAnyArgs().ModifyFile(null, null);
        }
    }
}
