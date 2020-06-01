﻿using System;
using System.IO;
using System.Text;
using Calamari.Commands.Support;
using Calamari.Common.Features.Scripting;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Variables;
using FluentAssertions;
using NSubstitute;
using NSubstitute.Core.Arguments;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ConfiguredScriptConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IScriptEngine scriptEngine;
        ICommandLineRunner commandLineRunner;
        RunningDeployment deployment;
        IVariables variables;
        const string stagingDirectory = "c:\\applications\\acme\\1.0.0";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            scriptEngine = Substitute.For<IScriptEngine>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();

            scriptEngine.GetSupportedTypes().Returns(new[] { ScriptSyntax.PowerShell });

            variables = new CalamariVariables();
            variables.Set(SpecialVariables.Package.EnabledFeatures, SpecialVariables.Features.CustomScripts);

            deployment = new RunningDeployment("C:\\packages", variables) { StagingDirectory = stagingDirectory };
        }

        [Test]
        public void ShouldRunScriptAtAppropriateStage()
        {
            const string stage = DeploymentStages.PostDeploy;
            const string scriptBody = "lorem ipsum blah blah blah";
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, ScriptSyntax.PowerShell);
            var scriptPath = Path.Combine(stagingDirectory, scriptName);
            var script = new Script(scriptPath);
            variables.Set(scriptName, scriptBody);

            var convention = CreateConvention();
            scriptEngine.Execute(Arg.Any<Script>(), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment, stage);

            fileSystem.Received().WriteAllBytes(scriptPath, Arg.Any<byte[]>());
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == scriptPath), variables, commandLineRunner);
        }

        [Test]
        public void ShouldRemoveScriptFileAfterRunning()
        {
            const string stage = DeploymentStages.PostDeploy;
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, ScriptSyntax.PowerShell);
            var scriptPath = Path.Combine(stagingDirectory, scriptName);
            var script = new Script(scriptPath);
            variables.Set(scriptName, "blah blah");

            var convention = CreateConvention();
            scriptEngine.Execute(Arg.Any<Script>(), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment, stage);

            fileSystem.Received().DeleteFile(scriptPath, Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldNotRemoveCustomPostDeployScriptFileAfterRunningIfSpecialVariableIsSet()
        {
            deployment.Variables.Set(SpecialVariables.DeleteScriptsOnCleanup, false.ToString());
            const string stage = DeploymentStages.PostDeploy;
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, ScriptSyntax.PowerShell);
            var scriptPath = Path.Combine(stagingDirectory, scriptName);
            variables.Set(scriptName, "blah blah");

            var convention = CreateConvention();
            scriptEngine.Execute(Arg.Any<Script>(), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment, stage);

            fileSystem.DidNotReceive().DeleteFile(scriptPath, Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldThrowAnErrorIfAScriptExistsWithTheWrongType()
        {
            const string stage = DeploymentStages.PostDeploy;
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, ScriptSyntax.CSharp);
            variables.Set(scriptName, "blah blah");
            var convention = CreateConvention();
            Action exec = () => convention.Install(deployment, stage);
            exec.Should().Throw<CommandException>().WithMessage("CSharp scripts are not supported on this platform (PostDeploy)");
        }

        private ConfiguredScriptService CreateConvention()
        {
            return new ConfiguredScriptService(fileSystem, scriptEngine, commandLineRunner);
        }
    }
}