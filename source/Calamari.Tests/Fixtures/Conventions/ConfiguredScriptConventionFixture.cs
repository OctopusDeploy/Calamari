using System.IO;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ConfiguredScriptConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IScriptEngineSelector scriptEngineSelector;
        IScriptEngine scriptEngine;
        ICommandLineRunner commandLineRunner;
        RunningDeployment deployment;
        VariableDictionary variables;
        const string stagingDirectory = "c:\\applications\\acme\\1.0.0";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            scriptEngineSelector = Substitute.For<IScriptEngineSelector>();
            scriptEngine = Substitute.For<IScriptEngine>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();

            scriptEngineSelector.GetSupportedExtensions().Returns(new string[] { "ps1" });
            scriptEngineSelector.SelectEngine(Arg.Any<string>()).Returns(scriptEngine);

            variables = new VariableDictionary();
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
            variables.Set(scriptName, scriptBody);

            var convention = CreateConvention(stage);
            scriptEngine.Execute(scriptPath, variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

            fileSystem.Received().OverwriteFile(scriptPath, scriptBody);
            scriptEngine.Received().Execute(scriptPath, variables, commandLineRunner);
        }

        [Test]
        public void ShouldRemoveScriptFileAfterRunning()
        {
            const string stage = DeploymentStages.PostDeploy;
            var scriptName = ConfiguredScriptConvention.GetScriptName(stage, "ps1");
            var scriptPath = Path.Combine(stagingDirectory, scriptName);
            variables.Set(scriptName, "blah blah");

            var convention = CreateConvention(stage);
            scriptEngine.Execute(scriptPath, variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

            fileSystem.Received().DeleteFile(scriptPath, Arg.Any<DeletionOptions>());
        }

        private ConfiguredScriptConvention CreateConvention(string deployStage)
        {
            return new ConfiguredScriptConvention(deployStage, scriptEngineSelector, fileSystem, commandLineRunner);
        }
    }
}