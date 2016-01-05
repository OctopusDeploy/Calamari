using System;
using System.Collections;
using System.IO;
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

        [SetUp]
        public void SetUp()
        {
            fileSystem = new WindowsPhysicalFileSystem();
            configurationTransformer = Substitute.For<IConfigurationTransformer>();

            var deployDirectory = BuildConfigPath(null);

            variables = new CalamariVariableDictionary();
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, deployDirectory);

            deployment = new RunningDeployment(deployDirectory, variables);
        }

        [Test]
        public void ShouldApplyReleaseTransformIfAutomaticallyRunConfigurationTransformationFilesFlagIsSet()
        {
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());

            CreateConvention().Install(deployment);

            AssertTransformRun("bar.config", "bar.Release.config");
        }

        [Test]
        public void ShouldNotApplyReleaseTransformIfAutomaticallyRunConfigurationTransformationFilesFlagNotSet()
        {
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());

            CreateConvention().Install(deployment);

            AssertTransformNotRun("bar.config", "bar.Release.config");
        }

        [Test]
        public void ShouldApplyEnvironmentTransform()
        {
            const string environment = "Production";

            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());
            variables.Set(SpecialVariables.Environment.Name, environment);

            CreateConvention().Install(deployment);

            AssertTransformRun("bar.config", "bar.Production.config");
        }

        [Test]
        public void ShouldApplySpecificCustomTransform()
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "foo.bar.config => foo.config");
            // This will be applied even if the automatically run flag is set to false
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());

            CreateConvention().Install(deployment);

            AssertTransformRun("foo.config", "foo.bar.config");
        }

        [Test]
        [TestCaseSource(nameof(AdvancedTransformTestCases))]
        public void ShouldApplyAdvancedTransformations(string sourceFile, string transformDefinition, string expectedAppliedTransform)
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, transformDefinition.Replace('\\', Path.DirectorySeparatorChar));            
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());

            CreateConvention().Install(deployment);

            AssertTransformRun(sourceFile, expectedAppliedTransform);
            configurationTransformer.ReceivedWithAnyArgs(1).PerformTransform("", "", ""); // Only Called Once
        }

        [Test]
        public void ShouldApplyMultipleWildcardsToSourceFile()
        {
            variables.Set(SpecialVariables.Package.AdditionalXmlConfigurationTransforms, "*.bar.blah => bar.blah");
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, false.ToString());

            CreateConvention().Install(deployment);

            AssertTransformRun("bar.blah", "foo.bar.blah");
            AssertTransformRun("bar.blah", "xyz.bar.blah");
            configurationTransformer.ReceivedWithAnyArgs(2).PerformTransform("", "", "");
        }

        private static IEnumerable AdvancedTransformTestCases
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
                yield return new TestCaseData("config\\fizz.xml", "foo.bar.xml=>config\\fizz.xml", "foo.bar.xml");
                yield return new TestCaseData("config\\fizz.xml", "transform\\fizz.buzz.xml=>config\\fizz.xml", "transform\\fizz.buzz.xml");
                yield return new TestCaseData("config\\fizz.xml", "transform\\*.xml=>config\\*.xml", "transform\\fizz.xml");
                yield return new TestCaseData("foo.config", "transform\\*.config=>foo.config", "transform\\fizz.config");
            }
        }

        private ConfigurationTransformsConvention CreateConvention()
        {
            return new ConfigurationTransformsConvention(fileSystem, configurationTransformer);
        }

        private void AssertTransformRun(string configFile, string transformFile)
        {
            configurationTransformer.Received().PerformTransform(BuildConfigPath(configFile), BuildConfigPath(transformFile), BuildConfigPath(configFile));
        }

        private void AssertTransformNotRun(string configFile, string transformFile)
        {
            configurationTransformer.DidNotReceive().PerformTransform(BuildConfigPath(configFile), BuildConfigPath(transformFile), BuildConfigPath(configFile));
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