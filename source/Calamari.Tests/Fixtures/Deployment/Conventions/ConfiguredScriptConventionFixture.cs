using System;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Conventions
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
            variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);

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

            var convention = CreateConvention(stage);
            scriptEngine.Execute(Arg.Any<Script>(), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

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

            var convention = CreateConvention(stage);
            scriptEngine.Execute(Arg.Any<Script>(), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

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
            scriptEngine.Execute(Arg.Any<Script>(), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

            fileSystem.DidNotReceive().DeleteFile(scriptPath, Arg.Any<FailureOptions>());
        }

        [Test]
        public void ShouldThrowAnErrorIfAScriptExistsWithTheWrongType()
        {
            const string stage = DeploymentStages.PostDeploy;
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, ScriptSyntax.CSharp);
            variables.Set(scriptName, "blah blah");
            var convention = CreateConvention(stage);
            Action exec = () => convention.Install(deployment);
            exec.Should().Throw<CommandException>().WithMessage("CSharp scripts are not supported on this platform (PostDeploy)");
        }

        private ConfiguredScriptConvention CreateConvention(string deployStage)
        {
            ConfiguredScriptBehaviour scriptBehaviour = null;
            if (deployStage == DeploymentStages.PreDeploy)
                scriptBehaviour = new PreDeployConfiguredScriptBehaviour(new InMemoryLog(), fileSystem, scriptEngine, commandLineRunner);
            else if (deployStage == DeploymentStages.Deploy)
                scriptBehaviour = new DeployConfiguredScriptBehaviour(new InMemoryLog(), fileSystem, scriptEngine, commandLineRunner);
            else if (deployStage == DeploymentStages.PostDeploy)
                scriptBehaviour = new PostDeployConfiguredScriptBehaviour(new InMemoryLog(), fileSystem, scriptEngine, commandLineRunner);
            return new ConfiguredScriptConvention(scriptBehaviour);
        }
    }
}