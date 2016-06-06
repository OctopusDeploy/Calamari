using System.Collections.Generic;
using System.IO;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class FeatureScriptConventionFixture
    {
        ICalamariFileSystem fileSystem;
        ICalamariEmbeddedResources embeddedResources;
        IScriptEngine scriptEngine;
        ICommandLineRunner commandLineRunner;
        RunningDeployment deployment;
        CalamariVariableDictionary variables;
        const string stagingDirectory = "c:\\applications\\acme\\1.0.0";
        const string scriptContents = "blah blah blah";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            embeddedResources = Substitute.For<ICalamariEmbeddedResources>();
            scriptEngine = Substitute.For<IScriptEngine>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();

            scriptEngine.GetSupportedExtensions().Returns(new string[] {"ps1"});

            variables = new CalamariVariableDictionary();
            variables.Set(SpecialVariables.Package.EnabledFeatures, "Octopus.Features.blah");

            deployment = new RunningDeployment("C:\\packages", variables) { StagingDirectory = stagingDirectory };
        }

        [Test]
        public void ShouldRunMatchingScripts()
        {
            const string suffix = "AfterPreDeploy";
            var features = new string[] {"feature1", "feature2"};

            Arrange(features, suffix);

            var convention = CreateConvention(suffix);
            convention.Install(deployment);

            foreach (var feature in features)
            {
                var script = new Script(Path.Combine(stagingDirectory, FeatureScriptConvention.GetScriptName(feature, suffix, "ps1")));
                scriptEngine.Received().Execute(script, variables, commandLineRunner);
            }
        }

        [Test]
        public void ShouldCreateScriptFileIfNotExists()
        {
            const string deployStage = "BeforePostDeploy";
            const string feature = "doTheThing";
            var scriptPath = Path.Combine(stagingDirectory, FeatureScriptConvention.GetScriptName(feature, deployStage, "ps1")); 
            variables.Set(SpecialVariables.Package.EnabledFeatures, feature);

            Arrange(new List<string>{ feature }, deployStage);
            fileSystem.FileExists(scriptPath).Returns(false);

            var convention = CreateConvention(deployStage);
            scriptEngine.Execute(new Script(scriptPath), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

            fileSystem.Received().OverwriteFile(scriptPath, scriptContents );
        }

        [Test]
        public void ShouldNotOverwriteScriptFileIfItExists()
        {
            const string deployStage = "BeforeDeploy";
            const string feature = "doTheThing";
            var scriptPath = Path.Combine(stagingDirectory, FeatureScriptConvention.GetScriptName(feature, deployStage, "ps1")); 

            Arrange(new List<string>{ feature }, deployStage);
            fileSystem.FileExists(scriptPath).Returns(true);

            var convention = CreateConvention(deployStage);

            scriptEngine.Execute(new Script(scriptPath), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

            fileSystem.DidNotReceive().OverwriteFile(scriptPath, Arg.Any<string>() );
        }

        [Test]
        public void ShouldDeleteScriptFile()
        {
            const string deployStage = "BeforeDeploy";
            const string feature = "doTheThing";
            var scriptPath = Path.Combine(stagingDirectory, FeatureScriptConvention.GetScriptName(feature, deployStage, "ps1")); 

            Arrange(new List<string>{ feature }, deployStage);
            var convention = CreateConvention(deployStage);

            scriptEngine.Execute(new Script(scriptPath), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);
            fileSystem.Received().DeleteFile(scriptPath, Arg.Any<FailureOptions>());
        }

        private void Arrange(ICollection<string> features, string suffix)
        {
            variables.Set(SpecialVariables.Package.EnabledFeatures, string.Join(",", features));

            var embeddedResourceNames = new List<string>();

            foreach (var feature in features)
            {
                var scriptName = FeatureScriptConvention.GetScriptName(feature, suffix, "ps1");
                var embeddedResourceName = FeatureScriptConvention.GetEmbeddedResourceName(scriptName);
                embeddedResources.GetEmbeddedResourceText(embeddedResourceName).Returns(scriptContents);
                embeddedResourceNames.Add(embeddedResourceName);
                var scriptPath = Path.Combine(stagingDirectory, scriptName);
                scriptEngine.Execute(new Script(scriptPath), variables, commandLineRunner).Returns(new CommandResult("", 0));
            }

            embeddedResources.GetEmbeddedResourceNames().Returns(embeddedResourceNames.ToArray());
        }

        private FeatureScriptConvention CreateConvention(string deployStage)
        {
            return new FeatureScriptConvention(deployStage, fileSystem, embeddedResources, scriptEngine, commandLineRunner );
        }
    }
}