using System;
using System.IO;
using System.Text;
using Calamari.Commands;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;
using Calamari.Shared;
using Calamari.Shared.FileSystem;
using Calamari.Shared.Scripting;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Script = Calamari.Integration.Scripting.Script;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ConfiguredScriptConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IScriptRunner scriptEngine;
        CalamariExecutionContext deployment;
        CalamariVariableDictionary variables;
        const string stagingDirectory = "c:\\applications\\acme\\1.0.0";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            scriptEngine = Substitute.For<IScriptRunner>();

            scriptEngine.GetSupportedTypes().Returns(new[] { ScriptSyntax.PowerShell });

            variables = new CalamariVariableDictionary();
            variables.Set(SpecialVariables.Package.EnabledFeatures, SpecialVariables.Features.CustomScripts);

            deployment = new CalamariExecutionContext("C:\\packages", variables) { StagingDirectory = stagingDirectory };
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

            var convention = CreateConvention(stage);
            scriptEngine.Execute(Arg.Any<Shared.Scripting.Script>()).Returns(new CommandResult("", 0));
            convention.Run(deployment);

<<<<<<< HEAD
            fileSystem.Received().OverwriteFile(scriptPath, scriptBody, Encoding.UTF8);
            scriptEngine.Received().Execute(Arg.Is<Shared.Scripting.Script>(s => s.File == scriptPath));
=======
            fileSystem.Received().WriteAllBytes(scriptPath, Arg.Any<byte[]>());
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == scriptPath), variables, commandLineRunner);
>>>>>>> vNext
        }

        [Test]
        public void ShouldRemoveScriptFileAfterRunning()
        {
            const string stage = DeploymentStages.PostDeploy;
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, ScriptSyntax.PowerShell);
            var scriptPath = Path.Combine(stagingDirectory, scriptName);
            var script = new Script(scriptPath);
            variables.Set(scriptName, "blah blah");

            var convention = CreateConvention(stage);
            scriptEngine.Execute(Arg.Any<Shared.Scripting.Script>()).Returns(new CommandResult("", 0));
            convention.Run(deployment);

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

            var convention = CreateConvention(stage);
            scriptEngine.Execute(Arg.Any<Shared.Scripting.Script>()).Returns(new CommandResult("", 0));
            convention.Run(deployment);

            fileSystem.DidNotReceive().DeleteFile(scriptPath, Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldThrowAnErrorIfAScriptExistsWithTheWrongType()
        {
            const string stage = DeploymentStages.PostDeploy;
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, ScriptSyntax.CSharp);
            variables.Set(scriptName, "blah blah");
            var convention = CreateConvention(stage);
            Action exec = () => convention.Run(deployment);
            exec.ShouldThrow<CommandException>().WithMessage("CSharp scripts are not supported on this platform (PostDeploy)");
        }

        private ConfiguredScriptConvention CreateConvention(string deployStage)
        {
            return new ConfiguredScriptConvention(deployStage, fileSystem, scriptEngine);
        }
    }
}