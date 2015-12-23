using System;
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
            fileSystem.GetRelativePath(Arg.Any<string>(), Arg.Any<string>()).Returns(x =>
                new WindowsPhysicalFileSystem().GetRelativePath((string) x[0], (string) x[1]));
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
            var deployDirectory = BuildConfigPath(null);
            var currentDeployment = new RunningDeployment(deployDirectory, variables);
            var physicalFileSystem = new WindowsPhysicalFileSystem();

            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, deployDirectory);
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, transformDefinition);
            // This will be applied even if the automatically run flag is set to false
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());

            var target = new ConfigurationTransformsConvention(physicalFileSystem, configurationTransformer);
            target.Install(currentDeployment);

            configurationTransformer.Received().PerformTransform(BuildConfigPath(sourceFile), BuildConfigPath(expectedAppliedTransform), BuildConfigPath(sourceFile));
            configurationTransformer.ReceivedWithAnyArgs(1).PerformTransform("", "", ""); // Only Called Once
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

        [Test]
        public void ShouldApplyMultipleWildcardsToSourceFile()
        {
            var deployDirectory = BuildConfigPath(null);
            var currentDeployment = new RunningDeployment(deployDirectory, variables);
            var physicalFileSystem = new WindowsPhysicalFileSystem();

            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, deployDirectory);
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "*.bar.blah => bar.blah");
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());

            var target = new ConfigurationTransformsConvention(physicalFileSystem, configurationTransformer);
            target.Install(currentDeployment);

            configurationTransformer.Received().PerformTransform(BuildConfigPath("bar.blah"), BuildConfigPath("foo.bar.blah"), BuildConfigPath("bar.blah"));
            configurationTransformer.Received().PerformTransform(BuildConfigPath("bar.blah"), BuildConfigPath("xyz.bar.blah"), BuildConfigPath("bar.blah"));
            configurationTransformer.ReceivedWithAnyArgs(2).PerformTransform("", "", "");
        }

        private IEnumerable AdvancedTransformTestCases
        {
            get
            {
                yield return new TestCaseData("bar.sitemap", "config\\fizz.sitemap.config=>bar.sitemap", "config\\fizz.sitemap.config");
                yield return new TestCaseData("bar.config", "config\\fizz.buzz.config=>bar.config", "config\\fizz.buzz.config");
                yield return new TestCaseData("bar.config", "foo.config=>bar.config", "foo.config");
                yield return new TestCaseData("bar.blah", "foo.baz=>bar.blah", "foo.baz");
                yield return new TestCaseData("bar.config", "foo.xml=>bar.config", "foo.xml");
                yield return new TestCaseData("xyz.bar.blah", "*.foo.blah=>*.bar.blah", "xyz.foo.blah");
                yield return new TestCaseData("foo.bar.blah", "foo.blah=>*.bar.blah", "foo.blah");
                yield return new TestCaseData("bar.blah", "*.bar.config=>bar.blah", "foo.bar.config");
                yield return new TestCaseData("foo.config", "foo.bar.additional.config=>foo.config", "foo.bar.additional.config");
                yield return new TestCaseData("foo.config", "*.bar.config=>*.config", "foo.bar.config");
                yield return new TestCaseData("foo.xml", "*.bar.xml=>*.xml", "foo.bar.xml");
                yield return new TestCaseData("config\\fizz.xml", "foo.Bar.xml=>config\\fizz.xml", "foo.bar.xml");
                yield return new TestCaseData("config\\fizz.xml", "transform\\fizz.buzz.xml=>config\\fizz.xml", "transform\\fizz.buzz.xml");
                yield return new TestCaseData("config\\fizz.xml", "transform\\*.xml=>config\\*.xml", "transform\\fizz.xml");
                yield return new TestCaseData("foo.config", "transform\\*.config=>foo.config", "transform\\fizz.config");
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
                fileSystem.DirectoryExists(Path.GetDirectoryName(file)).Returns(true);
                fileSystem.EnumerateFiles(Path.GetDirectoryName(file), Arg.Is<string[]>(s => s.Contains(StripPathFromTransformFile(file)))).Returns(new[] {file});                
            }
        }
        private static string StripPathFromTransformFile(string transformFile)
        {
            return transformFile.Contains(Path.DirectorySeparatorChar)
                ? transformFile.Substring(transformFile.LastIndexOf(Path.DirectorySeparatorChar)).Trim(Path.DirectorySeparatorChar)
                : transformFile;
        }

        private string BuildConfigPath(string filename)
        {
            var path = GetType().Namespace.Replace("Calamari.Tests.", String.Empty);
            path = path.Replace('.', Path.DirectorySeparatorChar);
            var workingDirectory = Path.Combine(TestEnvironment.CurrentWorkingDirectory, path, "ConfigTransforms");

            if (string.IsNullOrEmpty(filename))
                return workingDirectory;

            return Path.Combine(workingDirectory, filename.Replace('\\', Path.DirectorySeparatorChar));
        }
    }
}