using System;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class JsonConfigurationVariablesConventionFixture
    {
        void RunTest(
            bool jsonVariablesEnabled,
            bool structuredVariablesEnabled,
            Action<IStructuredConfigVariablesService> assertions
        )
        {
            var variables = new CalamariVariables();
            if (jsonVariablesEnabled)
            {
                variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
            }

            if (structuredVariablesEnabled)
            {
                variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
            }
            
            var service = Substitute.For<IStructuredConfigVariablesService>();
            var convention = new JsonConfigurationVariablesConvention(service);
            var deployment = new RunningDeployment("", variables);
            
            convention.Install(deployment);
            assertions(service);
        }

        [Test]
        public void ShouldCallDoJsonVariableReplacementIfJsonVariableIsSet()
        {
            RunTest(
                jsonVariablesEnabled: true,
                structuredVariablesEnabled: false,
                service => service.Received()
                    .DoJsonVariableReplacement(Arg.Any<RunningDeployment>())
            );
        }
        
        [Test]
        public void ShouldCallDoStructuredVariableReplacementIfStructuredVariableIsSet()
        {
            RunTest(
                jsonVariablesEnabled: false,
                structuredVariablesEnabled: true,
                service => service.Received()
                    .DoStructuredVariableReplacement(Arg.Any<RunningDeployment>())
            );
        }

        [Test]
        public void ShouldNotCallServiceIfNeitherJsonNorStructuredVariablesAreSet()
        {
            RunTest(
                jsonVariablesEnabled: false,
                structuredVariablesEnabled: false,
                service => service.DidNotReceive()
                    .DoStructuredVariableReplacement(Arg.Any<RunningDeployment>())
            );
        }

        [Test]
        public void ShouldPreferJsonOverStructuredVariables()
        {
            RunTest(
                jsonVariablesEnabled: true,
                structuredVariablesEnabled: true,
                service =>
                {
                    service.Received()
                        .DoJsonVariableReplacement(Arg.Any<RunningDeployment>());
                    
                    service.DidNotReceive()
                        .DoStructuredVariableReplacement(Arg.Any<RunningDeployment>());
                });
        }

        //
        // RunningDeployment deployment;
        // IStructuredConfigVariableReplacer configVariableReplacer;
        // ICalamariFileSystem fileSystem;
        // const string StagingDirectory = "C:\\applications\\Acme\\1.0.0";
        //
        // [SetUp]
        // public void SetUp()
        // {
        //     var variables = new CalamariVariables(); 
        //     variables.Set(KnownVariables.OriginalPackageDirectoryPath, TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0"));
        //     deployment = new RunningDeployment(TestEnvironment.ConstructRootedPath("Packages"), variables);
        //     configVariableReplacer = Substitute.For<IStructuredConfigVariableReplacer>();
        //     fileSystem = Substitute.For<ICalamariFileSystem>();
        //     fileSystem.DirectoryExists(Arg.Any<string>()).Returns(false);
        // }
        //
        // [Test]
        // public void ShouldNotRunIfVariableNotSet()
        // {
        //     var convention = new JsonConfigurationVariablesConvention(configVariableReplacer, fileSystem);
        //     convention.Install(deployment);
        //     configVariableReplacer.DidNotReceiveWithAnyArgs().ModifyFile(null, null);
        // }
        //
        // [Test]
        // public void ShouldFindAndCallModifyOnTargetFile()
        // {
        //     fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), "appsettings.environment.json")
        //         .Returns(new[] {TestEnvironment.ConstructRootedPath("applications" ,"Acme", "1.0.0", "appsettings.environment.json")});
        //
        //     deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesEnabled, "true");
        //     deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesTargets, "appsettings.environment.json");
        //     var convention = new JsonConfigurationVariablesConvention(configVariableReplacer, fileSystem);
        //     convention.Install(deployment);
        //     configVariableReplacer.Received().ModifyFile(TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", "appsettings.environment.json"), deployment.Variables);
        // }
        //
        // [Test]
        // public void ShouldFindAndCallModifyOnAllTargetFiles()
        // {
        //     var targetFiles = new[]
        //     {
        //         "config.json",
        //         "config.dev.json",
        //         "config.prod.json"
        //     };
        //
        //     fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), "config.json")
        //         .Returns(new[] {targetFiles[0]}.Select(t => TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", t)));
        //     fileSystem.EnumerateFilesWithGlob(Arg.Any<string>(), "config.*.json")
        //         .Returns(targetFiles.Skip(1).Select(t => TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", t)));
        //
        //     deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesEnabled, "true");
        //     deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesTargets, string.Join(Environment.NewLine, "config.json", "config.*.json"));
        //
        //     var convention = new JsonConfigurationVariablesConvention(configVariableReplacer, fileSystem);
        //     convention.Install(deployment);
        //
        //     foreach (var targetFile in targetFiles)
        //     {
        //         configVariableReplacer.Received()
        //             .ModifyFile(TestEnvironment.ConstructRootedPath("applications", "Acme", "1.0.0", targetFile), deployment.Variables);
        //     }
        // }
        //
        // [Test]
        // public void ShouldNotAttemptToRunOnDirectories()
        // {
        //     deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesEnabled, "true");
        //     deployment.Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesTargets, "approot");
        //     fileSystem.DirectoryExists(Arg.Any<string>()).Returns(true);
        //
        //     var convention = new JsonConfigurationVariablesConvention(configVariableReplacer, fileSystem);
        //     convention.Install(deployment);
        //     configVariableReplacer.DidNotReceiveWithAnyArgs().ModifyFile(null, null);
        // }
    }
}
