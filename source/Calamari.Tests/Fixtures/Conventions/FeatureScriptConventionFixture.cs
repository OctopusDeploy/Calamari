using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.EmbeddedResources;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using Calamari.Variables;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class FeatureScriptConventionFixture
    {
        ICalamariFileSystem fileSystem;
        ICalamariEmbeddedResources embeddedResources;
        ICombinedScriptEngine scriptEngine;
        ICommandLineRunner commandLineRunner;
        RunningDeployment deployment;
        IVariables variables;
        const string stagingDirectory = "c:\\applications\\acme\\1.0.0";
        const string scriptContents = "blah blah blah";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            embeddedResources = Substitute.For<ICalamariEmbeddedResources>();
            scriptEngine = Substitute.For<ICombinedScriptEngine>();
            commandLineRunner = Substitute.For<ICommandLineRunner>();

            scriptEngine.GetSupportedTypes().Returns(new[] { ScriptSyntax.PowerShell });

            variables = new CalamariVariables();
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
                var scriptPath = Path.Combine(stagingDirectory, FeatureConvention.GetScriptName(feature, suffix, "ps1"));
                scriptEngine.Received().Execute(Arg.Is<Script>(s => s.File == scriptPath), variables, commandLineRunner);
            }
        }

        [Test]
        public void ShouldCreateScriptFileIfNotExists()
        {
            const string deployStage = "BeforePostDeploy";
            const string feature = "doTheThing";
            var scriptPath = Path.Combine(stagingDirectory, FeatureConvention.GetScriptName(feature, deployStage, "ps1")); 
            variables.Set(SpecialVariables.Package.EnabledFeatures, feature);

            Arrange(new List<string>{ feature }, deployStage);
            fileSystem.FileExists(scriptPath).Returns(false);

            var convention = CreateConvention(deployStage);
            scriptEngine.Execute(Arg.Is<Script>(s => s.File == scriptPath), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

            fileSystem.Received().OverwriteFile(scriptPath, scriptContents );
        }

        [Test]
        public void ShouldNotOverwriteScriptFileIfItExists()
        {
            const string deployStage = "BeforeDeploy";
            const string feature = "doTheThing";
            var scriptPath = Path.Combine(stagingDirectory, FeatureConvention.GetScriptName(feature, deployStage, "ps1")); 

            Arrange(new List<string>{ feature }, deployStage);
            fileSystem.FileExists(scriptPath).Returns(true);

            var convention = CreateConvention(deployStage);

            scriptEngine.Execute(Arg.Is<Script>(s => s.File == scriptPath), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);

            fileSystem.DidNotReceive().OverwriteFile(scriptPath, Arg.Any<string>() );
        }

        [Test]
        public void ShouldDeleteScriptFile()
        {
            const string deployStage = "BeforeDeploy";
            const string feature = "doTheThing";
            var scriptPath = Path.Combine(stagingDirectory, FeatureConvention.GetScriptName(feature, deployStage, "ps1")); 

            Arrange(new List<string>{ feature }, deployStage);
            var convention = CreateConvention(deployStage);

            scriptEngine.Execute(Arg.Is<Script>(s => s.File == scriptPath), variables, commandLineRunner).Returns(new CommandResult("", 0));
            convention.Install(deployment);
            fileSystem.Received().DeleteFile(scriptPath, Arg.Any<FailureOptions>());
        }

        private void Arrange(ICollection<string> features, string suffix)
        {
            variables.Set(SpecialVariables.Package.EnabledFeatures, string.Join(",", features));

            var embeddedResourceNames = new List<string>();

            foreach (var feature in features)
            {
                var scriptName = FeatureConvention.GetScriptName(feature, suffix, "ps1");
                var embeddedResourceName = FeatureConvention.GetEmbeddedResourceName(scriptName);
                embeddedResources.GetEmbeddedResourceText(Arg.Any<Assembly>(), embeddedResourceName).Returns(scriptContents);
                embeddedResourceNames.Add(embeddedResourceName);
                var scriptPath = Path.Combine(stagingDirectory, scriptName);
                scriptEngine.Execute(Arg.Is<Script>(s => s.File == scriptPath), variables, commandLineRunner)
                            .Returns(new CommandResult("", 0));
            }

            embeddedResources.GetEmbeddedResourceNames(Arg.Any<Assembly>()).Returns(embeddedResourceNames.ToArray());
        }

        private FeatureConvention CreateConvention(string deployStage)
        {
            return new FeatureConvention(deployStage, null, fileSystem, scriptEngine, commandLineRunner, embeddedResources);
        }
    }
}