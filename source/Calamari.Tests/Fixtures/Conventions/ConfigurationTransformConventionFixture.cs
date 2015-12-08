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
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ConfigurationTransformConventionFixture
    {
        ICalamariFileSystem fileSystem;
        IConfigurationTransformer configurationTransformer;
        RunningDeployment deployment;
        CalamariVariableDictionary variables;
        const string stagingDirectory = "c:\\applications\\acme\\1.0.0";
        ProxyLog logs;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            configurationTransformer = Substitute.For<IConfigurationTransformer>();

            variables = new CalamariVariableDictionary();
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, stagingDirectory);

            deployment = new RunningDeployment("C:\\packages", variables);
            logs = new ProxyLog();
        }

        [TearDown]
        public void TearDown()
        {
            logs.Dispose();
        }

        [Test]
        public void ShouldApplyReleaseTransformIfAutomaticallyRunConfigurationTransformationFilesFlagIsSet()
        {
            var webConfig = Path.Combine(stagingDirectory, "web.config");
            var webConfigReleaseTransform = Path.Combine(stagingDirectory, "web.Release.config");

            MockSearchableFiles(fileSystem, stagingDirectory, new[] { webConfig, webConfigReleaseTransform }, "*.config");

            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.Received().PerformTransform(webConfig, webConfigReleaseTransform, webConfig);
        }

        [Test]
        public void ShouldNotApplyReleaseTransformIfAutomaticallyRunConfigurationTransformationFilesFlagNotSet()
        {
            var webConfig = Path.Combine(stagingDirectory, "web.config");
            var webConfigReleaseTransform = Path.Combine(stagingDirectory, "web.Release.config");

            MockSearchableFiles(fileSystem, stagingDirectory, new[] { webConfig, webConfigReleaseTransform }, "*.config");

            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.DidNotReceive().PerformTransform(webConfig, webConfigReleaseTransform, webConfig);
        }

        [Test]
        public void ShouldApplyEnvironmentTransform()
        {
            const string environment = "Production";
            var webConfig = Path.Combine(stagingDirectory, "web.config");
            var environmentTransform = Path.Combine(stagingDirectory, "web.Production.config");

            MockSearchableFiles(fileSystem, stagingDirectory, new[] { webConfig, environmentTransform }, "*.config");

            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());
            variables.Set(SpecialVariables.Environment.Name, environment);
            CreateConvention().Install(deployment);

            configurationTransformer.Received().PerformTransform(webConfig, environmentTransform, webConfig);
        }

        [Test]
        public void ShouldLogErrorIfUnableToFindFile()
        {
            var webConfig = Path.Combine(stagingDirectory, "web.config");

            MockSearchableFiles(fileSystem, stagingDirectory, new[] {webConfig}, "*.config");

            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "WebX.Release.config => Web.config");

            CreateConvention().Install(deployment);
            logs.AssertContains("The transform pattern \"WebX.Release.config => Web.config\" was not performed due to a missing file or overlapping rule.");
        }

        [Test]
        public void ShouldApplySpecificCustomTransform()
        {
            var webConfig = Path.Combine(stagingDirectory, "web.config");
            var specificTransform = Path.Combine(stagingDirectory, "web.Foo.config");

            MockSearchableFiles(fileSystem, stagingDirectory, new[] {webConfig, specificTransform}, "*.config");

            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "web.Foo.config => web.config");
            // This will be applied even if the automatically run flag is set to false
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.Received().PerformTransform(webConfig, specificTransform, webConfig);
            logs.AssertDoesNotContain("The transform pattern \"web.Foo.config => web.config\" was not performed due to a missing file or overlapping rule.");
        }

        [Test]
        [TestCaseSource(nameof(AdvancedTransformTestCases))]
        public void ShouldApplyAdvancedTransformations(string sourceFile, string transformDefinition, string expectedAppliedTransform)
        {
            MockSearchableFiles(fileSystem, stagingDirectory, new[] { sourceFile, expectedAppliedTransform }, "*" + Path.GetExtension(sourceFile));

            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, transformDefinition);
            // This will be applied even if the automatically run flag is set to false
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.Received().PerformTransform(sourceFile, expectedAppliedTransform, sourceFile);
            logs.AssertDoesNotContain("The transform pattern");
        }

        [Test]
        public void ShouldNotApplyAdvancedTransformationsInSubFolderWithWildcard()
        {
            var webConfig = Path.Combine(stagingDirectory, "web.config");
            var specificTransform = Path.Combine(stagingDirectory, "config\\web.Foo.config");

            MockSearchableFiles(fileSystem, stagingDirectory, new[] { webConfig, specificTransform }, "*.config");

            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "config\\*.Foo.config => web.config");
            // This will be applied even if the automatically run flag is set to false
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());
            CreateConvention().Install(deployment);

            configurationTransformer.DidNotReceive().PerformTransform(webConfig, specificTransform, webConfig);
            logs.AssertContains("The transform pattern \"config\\*.Foo.config => web.config\" was not performed due to a missing file or overlapping rule.");
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
                .TrimStart(Path.DirectorySeparatorChar);
        }

        private static string BuildConfigPath(string filename)
        {
            return TestEnvironment.ConstructRootedPath("random", "path", filename);
        }
    }
}