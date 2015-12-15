using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.ConfigurationTransforms;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ConfigurationTransformConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IConfigurationTransformer configurationTransformer;
        RunningDeployment deployment;
        CalamariVariableDictionary variables;
        const string StagingDirectory = "c:\\applications\\acme\\1.0.0";

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            configurationTransformer = Substitute.For<IConfigurationTransformer>();

            variables = new CalamariVariableDictionary();
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, StagingDirectory);

            deployment = new RunningDeployment("C:\\packages", variables);
        }

        [Test]
        public void ShouldApplyReleaseTransformIfAutomaticallyRunConfigurationTransformationFilesFlagIsSet()
        {
            var webConfig = Path.Combine(StagingDirectory, "web.config");
            var webConfigReleaseTransform = Path.Combine(StagingDirectory, "web.Release.config");

            MockSearchableFiles(fileSystem, StagingDirectory, new[] { webConfig, webConfigReleaseTransform }, "*.config");

            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.Received().PerformTransform(webConfig, webConfigReleaseTransform, webConfig);
        }

        [Test]
        public void ShouldNotApplyReleaseTransformIfAutomaticallyRunConfigurationTransformationFilesFlagNotSet()
        {
            var webConfig = Path.Combine(StagingDirectory, "web.config");
            var webConfigReleaseTransform = Path.Combine(StagingDirectory, "web.Release.config");

            MockSearchableFiles(fileSystem, StagingDirectory, new[] { webConfig, webConfigReleaseTransform }, "*.config");

            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.DidNotReceive().PerformTransform(webConfig, webConfigReleaseTransform, webConfig);
        }

        [Test]
        public void ShouldApplyEnvironmentTransform()
        {
            const string environment = "Production";
            var webConfig = Path.Combine(StagingDirectory, "web.config");
            var environmentTransform = Path.Combine(StagingDirectory, "web.Production.config");

            MockSearchableFiles(fileSystem, StagingDirectory, new[] { webConfig, environmentTransform }, "*.config");

            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());
            variables.Set(SpecialVariables.Environment.Name, environment);
            CreateConvention().Install(deployment);

            configurationTransformer.Received().PerformTransform(webConfig, environmentTransform, webConfig);
        }

        [Test]
        public void ShouldApplySpecificCustomTransform()
        {
            var webConfig = Path.Combine(StagingDirectory, "web.config");
            var specificTransform = Path.Combine(StagingDirectory, "web.Foo.config");

            MockSearchableFiles(fileSystem, StagingDirectory, new[] { webConfig, specificTransform }, "*.config");

            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "web.Foo.config => web.config");
            // This will be applied even if the automatically run flag is set to false
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.Received().PerformTransform(webConfig, specificTransform, webConfig);
        }

        [Test]
        [TestCaseSource(nameof(AdvancedTransformTestCases))]
        public void ShouldApplyAdvancedTransformations(string sourceFile, string transformDefinition, string expectedAppliedTransform)
        {
            var searchPattern = "*" + Path.GetExtension(sourceFile);
            MockSearchableFiles(fileSystem, StagingDirectory, new[] { sourceFile, expectedAppliedTransform }, searchPattern);

            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, transformDefinition);
            // This will be applied even if the automatically run flag is set to false
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.Received().PerformTransform(sourceFile, expectedAppliedTransform, sourceFile);
        }

        [Test]
        public void ShouldNotApplyAdvancedTransformationsInSubFolderWithWildcard()
        {
            var webConfig = Path.Combine(StagingDirectory, "web.config");
            var specificTransform = Path.Combine(StagingDirectory, "config\\web.Foo.config");

            MockSearchableFiles(fileSystem, StagingDirectory, new[] { webConfig, specificTransform }, "*.config");

            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "config\\*.Foo.config => web.config");
            // This will be applied even if the automatically run flag is set to false
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.DidNotReceive().PerformTransform(webConfig, specificTransform, webConfig);
        }

        [Test]
        public void ShouldApplyAdvancedTransformationToAppropriateFilesAndNotOtherTransformationFiles()
        {
            var newDoc = Path.Combine(StagingDirectory, "New Text Document.txt");
            var octoExe = Path.Combine(StagingDirectory, "Octo.exe");
            var octoExeConfig = Path.Combine(StagingDirectory, "Octo.exe.config");
            var somethingConfig = Path.Combine(StagingDirectory, "Something.config");
            var somethingReleaseConfig = Path.Combine(StagingDirectory, "Something.Release.config");
            var somethingReleaseXml = Path.Combine(StagingDirectory, "Something.Release.xml");
            var somethingTestConfig = Path.Combine(StagingDirectory, "Something.Test.config");
            var somethingTestXml = Path.Combine(StagingDirectory, "Something.Test.xml");
            var somethingXml = Path.Combine(StagingDirectory, "Something.xml");
            var webConfig = Path.Combine(StagingDirectory, "Web.config");
            var webReleaseConfig = Path.Combine(StagingDirectory, "Web.Release.config");

            MockSearchableFiles(fileSystem, StagingDirectory, new[] { newDoc, octoExe, octoExeConfig, somethingConfig, somethingReleaseConfig, somethingReleaseXml, somethingTestConfig, somethingTestXml, somethingXml, webConfig, webReleaseConfig }, "*.xml");

            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "*.Release.xml => *.xml");
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.Received().PerformTransform(somethingXml, somethingReleaseXml, somethingXml);
            configurationTransformer.DidNotReceive().PerformTransform(somethingTestXml, somethingReleaseXml, somethingTestXml);
            configurationTransformer.ReceivedWithAnyArgs(1).PerformTransform("", "", ""); // Only Called Once
        }

        private static IEnumerable AdvancedTransformTestCases
        {
            get
            {
                yield return new TestCaseData(BuildConfigPath("bar.sitemap"), "config\\foo.sitemap.config=>bar.sitemap", BuildConfigPath("config\\foo.sitemap.config"));
                yield return new TestCaseData(BuildConfigPath("bar.config"), "config\\foo.bar.config=>bar.config", BuildConfigPath("config\\foo.bar.config"));
                yield return new TestCaseData(BuildConfigPath("bar.config"), "foo.config=>bar.config", BuildConfigPath("foo.config"));
                yield return new TestCaseData(BuildConfigPath("bar.blah"), "foo.baz=>bar.blah", BuildConfigPath("foo.baz"));
                yield return new TestCaseData(BuildConfigPath("bar.config"), "foo.xml=>bar.config", BuildConfigPath("foo.xml"));
                yield return new TestCaseData(BuildConfigPath("xyz.bar.blah"), "*.foo.blah=>*.bar.blah", BuildConfigPath("xyz.foo.blah"));
                yield return new TestCaseData(BuildConfigPath("xyz.bar.blah"), "foo.blah=>*.bar.blah", BuildConfigPath("xyz.foo.blah"));
                yield return new TestCaseData(BuildConfigPath("xyz.bar.blah"), "*.foo.blah=>bar.blah", BuildConfigPath("xyz.foo.blah"));
                yield return new TestCaseData(BuildConfigPath("foo.config"), "foo.Bar.Additional.config=>foo.config", BuildConfigPath("foo.Bar.Additional.config"));
                yield return new TestCaseData(BuildConfigPath("foo.config"), "*.Bar.config=>*.config", BuildConfigPath("foo.Bar.config"));
                yield return new TestCaseData(BuildConfigPath("foo.xml"), "*.Bar.xml=>*.xml", BuildConfigPath("foo.Bar.xml"));
            }
        }

        private ConfigurationTransformsConvention CreateConvention()
        {
            return new ConfigurationTransformsConvention(fileSystem, configurationTransformer);
        }

        private static void MockSearchableFiles(ICalamariFileSystem fileSystem, string parentDirectory, string[] files, string searchPattern)
        {
            fileSystem.EnumerateFilesRecursively(parentDirectory,
                Arg.Is<string[]>(x => new List<string>(x).Contains(searchPattern))).Returns(files);

            foreach (var file in files)
            {
                fileSystem.FileExists(file).Returns(true);
                fileSystem.EnumerateFiles(Path.GetDirectoryName(files[0]), Arg.Is<string[]>(s => s.Contains(GetRelativePathToTransformFile(files[0], file)))).Returns(new[] {file});
            }
        }
        private static string GetRelativePathToTransformFile(string sourceFile, string transformFile)
        {
            return transformFile
                .Replace(Path.GetDirectoryName(sourceFile) ?? string.Empty, "")
                .TrimStart('\\');
        }

        private static string BuildConfigPath(string filename)
        {
            return TestEnvironment.ConstructRootedPath("random", "path", filename);
        }
    }
}