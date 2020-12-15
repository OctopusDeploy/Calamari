using System.IO;
using Assent;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    public class DeployPackageWithStructuredConfigurationFixture : DeployPackageFixture
    {
        const string ServiceName = "Acme.StructuredConfigFiles";
        const string ServiceVersion = "1.0.0";
        const string YamlFileName = "values.yaml";
        const string JsonFileName = "values.json";
        const string XmlFileName = "values.xml";
        const string ConfigFileName = "values.config";
        const string PropertiesFileName = "config.properties";
        const string MalformedFileName = "malformed.file";

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        [Test]
        public void FailsAndWarnsIfAFileCannotBeParsedWhenFallbackFlagIsSet()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.AddFlag(ActionVariables.StructuredConfigurationFallbackFlag, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, MalformedFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();
                result.AssertErrorOutput("The file could not be parsed as Json");
            }
        }

        [Test]
        public void ShouldNotTreatYamlFileAsYamlWhenFallbackFlagIsSet()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.AddFlag(ActionVariables.StructuredConfigurationFallbackFlag, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, YamlFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();

                // Indicates we tried to parse yaml as JSON.
                result.AssertErrorOutput("The file could not be parsed as Json");
            }
        }

        [Test]
        public void ShouldPerformReplacementInYaml()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, YamlFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedYamlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, YamlFileName));

                this.Assent(extractedPackageUpdatedYamlFile, TestEnvironment.AssentYamlConfiguration);
            }
        }

        [Test]
        public void ShouldPerformReplacementInXml()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, XmlFileName);
                Variables.Set("/document/key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedXmlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, XmlFileName));

                this.Assent(extractedPackageUpdatedXmlFile, TestEnvironment.AssentXmlConfiguration);
            }
        }

        [Test]
        public void IfThereAreDuplicateNsPrefixesTheFirstOneIsUsedAndAWarningIsLogged()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, "duplicate-prefixes.xml");
                Variables.Set("//parent/dupe:node", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                result.AssertOutputMatches("You can avoid this by ensuring all namespaces in your document have unique prefixes\\.");

                var extractedPackageUpdatedXmlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, "duplicate-prefixes.xml"));

                this.Assent(extractedPackageUpdatedXmlFile, TestEnvironment.AssentXmlConfiguration);
            }
        }

        [Test]
        public void LogsAWarningIfAVariableTreatedAsMarkupIsInvalid()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, "values.xml");
                Variables.Set("/document", "<<<");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                result.AssertOutputContains("Could not set the value of the XML element at XPath '/document' to '<<<'. Expected a valid XML fragment. Skipping replacement of this element.");

                var extractedPackageUpdatedXmlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, "values.xml"));

                this.Assent(extractedPackageUpdatedXmlFile, TestEnvironment.AssentXmlConfiguration);
            }
        }

        [Test]
        public void ShouldPerformReplacementInProperties()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, PropertiesFileName);
                Variables.Set("debug", "false");
                Variables.Set("port", "80");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedPropertiesFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, PropertiesFileName));

                this.Assent(extractedPackageUpdatedPropertiesFile, TestEnvironment.AssentPropertiesConfiguration);
            }
        }

        [Test]
        public void JsonShouldBeTriedBeforeOtherFormatsWhenGuessingTheBestFormat()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, YamlFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                result.AssertOutput("The file will be tried as Json first for backwards compatibility");
                result.AssertOutputMatches("Structured variable replacement succeeded on file .+? with format Yaml");
            }
        }

        [Test]
        public void SucceedsButWarnsIfNoFilesMatchTarget()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, "doesnt-exist.json");
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                result.AssertOutputContains("No files were found that match the replacement target pattern 'doesnt-exist.json'");
            }
        }

        [Test]
        public void FallsBackToJsonIfUnknownFileExtension()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, ConfigFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedConfigFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, ConfigFileName));

                this.Assent(extractedPackageUpdatedConfigFile, TestEnvironment.AssentJsonConfiguration);
            }
        }

        [Test]
        public void CanPerformReplacementOnMultipleTargetFiles()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, $"{JsonFileName}\n{YamlFileName}");
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedJsonFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, JsonFileName));
                var extractedPackageUpdatedYamlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, YamlFileName));

                this.Assent(extractedPackageUpdatedJsonFile, TestEnvironment.AssentJsonConfiguration);
                this.Assent(extractedPackageUpdatedYamlFile, TestEnvironment.AssentYamlConfiguration);
            }
        }

        [Test]
        public void CanPerformReplacementOnAGlobThatMatchesFilesInDifferentFormats()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, "values.*");
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedJsonFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, JsonFileName));
                var extractedPackageUpdatedYamlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, YamlFileName));
                var extractedPackageUpdatedConfigFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, ConfigFileName));

                this.Assent(extractedPackageUpdatedJsonFile, TestEnvironment.AssentJsonConfiguration);
                this.Assent(extractedPackageUpdatedYamlFile, TestEnvironment.AssentYamlConfiguration);
                this.Assent(extractedPackageUpdatedConfigFile, TestEnvironment.AssentJsonConfiguration);
            }
        }

        [Test]
        public void FailsIfAnyFileMatchingAGlobFailsToParse()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, "*.json");
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);

                result.AssertFailure();
                result.AssertErrorOutput("The file could not be parsed as Json");
            }
        }

        [Test]
        public void FailsIfAFileFailsToParseWhenThereAreMultipleTargetFiles()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, $"{JsonFileName}\n{MalformedFileName}");
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();
                result.AssertErrorOutput("The file could not be parsed as Json");
            }
        }

        [Test]
        public void SucceedsButWarnsIfTargetIsADirectory()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, ".");
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                result.AssertOutputContains("Skipping structured variable replacement on '.' because it is a directory.");

                var unchangedJsonFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, JsonFileName));
                var unchangedYamlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, YamlFileName));

                this.Assent(unchangedJsonFile, TestEnvironment.AssentJsonConfiguration);
                this.Assent(unchangedYamlFile, TestEnvironment.AssentYamlConfiguration);
            }
        }

        [Test]
        public void FailsAndWarnsIfAFileCannotBeParsed()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, MalformedFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();
                result.AssertErrorOutput("The file could not be parsed as Json");
            }
        }

        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }
    }
}