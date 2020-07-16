using System;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    [TestFixture]
    public class StructuredConfigVariablesServiceFixture
    {
        RunningDeployment deployment;
        ICalamariFileSystem fileSystem;
        IFileFormatVariableReplacer jsonReplacer;
        
        [SetUp]
        public void SetUp()
        {
            var variables = new CalamariVariables(); 
            variables.Set(KnownVariables.OriginalPackageDirectoryPath, TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0"));
            deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("Packages"), variables);
            fileSystem = Substitute.For<ICalamariFileSystem>();
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);
            jsonReplacer = Substitute.For<IFileFormatVariableReplacer>();
            jsonReplacer.SupportedFormat.Returns("Json");
        }
        
        [Test]
        public void ShouldFindAndCallModifyOnTargetFile()
        {
            fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), "appsettings.environment.json")
                .Returns(new[] {TestEnvironment.ConstructRootedPath("applications" ,"Acme", "1.0.0", "appsettings.environment.json")});
        
            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesEnabled, "true");
            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, "appsettings.environment.json");
            
            var replacers = new[]
            {
                jsonReplacer
            };

            var service = new StructuredConfigVariablesService(fileSystem, replacers);
            service.DoJsonVariableReplacement(deployment);
            jsonReplacer.Received().ModifyFile(TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", "appsettings.environment.json"), deployment.Variables);
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
        
            var replacers = new[]
            {
                jsonReplacer
            };

            var service = new StructuredConfigVariablesService(fileSystem, replacers);
            service.DoJsonVariableReplacement(deployment);
            
            foreach (var targetFile in targetFiles)
            {
                jsonReplacer.Received()
                    .ModifyFile(TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", targetFile), deployment.Variables);
            }
        }
        
        [Test]
        public void ShouldNotAttemptToRunOnDirectories()
        {
            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesEnabled, "true");
            deployment.Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, "approot");
            fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
        
            var replacers = new[]
            {
                jsonReplacer
            };

            var service = new StructuredConfigVariablesService(fileSystem, replacers);
            service.DoJsonVariableReplacement(deployment);
            
            jsonReplacer.DidNotReceiveWithAnyArgs().ModifyFile(null, null);
        }
    }
}