using System.IO;
using System.Text;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
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
        CalamariVariableDictionary variables;
        const string stagingDirectory = "c:\\applications\\acme\\1.0.0";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            scriptEngine = Substitute.For<IScriptEngine>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();

            scriptEngine.GetSupportedExtensions().Returns(new string[] { "ps1" });

            variables = new CalamariVariableDictionary();
            variables.Set(SpecialVariables.Package.EnabledFeatures, SpecialVariables.Features.CustomScripts);

            deployment = new RunningDeployment("C:\\packages", variables) { StagingDirectory = stagingDirectory };
        }

        [Test]
        public void ShouldRunScriptAtAppropriateStage()
        {
            const string stage = DeploymentStages.PostDeploy;
            const string scriptBody = "lorem ipsum blah blah blah";
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, "ps1");
            var scriptPath = Path.Combine(stagingDirectory, scriptName);
            var script = new Script(scriptPath);
            variables.Set(scriptName, scriptBody);

            var convention = CreateConvention(stage);
            scriptEngine.Execute(Arg.Any<Script>(), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

            fileSystem.Received().OverwriteFile(scriptPath, scriptBody, Encoding.UTF8);
            scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == scriptPath), variables, commandLineRunner);
        }

        [Test]
        public void ShouldRemoveScriptFileAfterRunning()
        {
            const string stage = DeploymentStages.PostDeploy;
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, "ps1");
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
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, "ps1");
            var scriptPath = Path.Combine(stagingDirectory, scriptName);
            variables.Set(scriptName, "blah blah");

            var convention = CreateConvention(stage);
            scriptEngine.Execute(Arg.Any<Script>(), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

            fileSystem.DidNotReceive().DeleteFile(scriptPath, Arg.Any<FailureOptions>());
        }

        private ConfiguredScriptConvention CreateConvention(string deployStage)
        {
            return new ConfiguredScriptConvention(deployStage, scriptEngine, fileSystem, commandLineRunner);
        }
    }
}