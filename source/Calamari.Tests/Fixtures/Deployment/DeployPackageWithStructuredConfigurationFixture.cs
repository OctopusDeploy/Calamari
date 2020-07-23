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
        const string ConfigFileName = "values.config";
        const string MalformedFileName = "malformed.file";

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }
        
        [Test]
        public void FailsAndWarnsIfAFileCannotBeParsedWhenFeatureFlagIsNotSet()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, MalformedFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();

                // Indicates the file couldn't be parsed.
                result.AssertErrorOutput("Newtonsoft.Json.JsonReaderException");
            }
        }
        
        [Test]
        public void ShouldNotTreatYamlFileAsYamlWhenFeatureFlagIsNotSet()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, YamlFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();

                // Indicates we tried to parse yaml as JSON.
                result.AssertErrorOutput("Newtonsoft.Json.JsonReaderException");
            }
        }
        
        [Test]
        public void ShouldPerformReplacementInYamlIfFlagIsSet()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, YamlFileName);
                Variables.AddFlag(ActionVariables.StructuredConfigurationFeatureFlag, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedYamlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, YamlFileName));

                this.Assent(extractedPackageUpdatedYamlFile, TestEnvironment.AssentYamlConfiguration);
            }
        }
        
        [Test]
        public void SucceedsButWarnsIfNoFilesMatchTarget()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, "doesnt-exist.json");
                Variables.AddFlag(ActionVariables.StructuredConfigurationFeatureFlag, true);
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
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, ConfigFileName);
                Variables.AddFlag(ActionVariables.StructuredConfigurationFeatureFlag, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                
                var extractedPackageUpdatedConfigFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, ConfigFileName));

                this.Assent(extractedPackageUpdatedConfigFile, TestEnvironment.AssentJsonDeepCompareConfiguration);
            }
        }
        
        [Test]
        public void CanPerformReplacementOnMultipleTargetFiles()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, $"{JsonFileName}\n{YamlFileName}");
                Variables.AddFlag(ActionVariables.StructuredConfigurationFeatureFlag, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                
                var extractedPackageUpdatedJsonFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, JsonFileName));
                var extractedPackageUpdatedYamlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, YamlFileName));

                this.Assent(extractedPackageUpdatedJsonFile, TestEnvironment.AssentJsonDeepCompareConfiguration);
                this.Assent(extractedPackageUpdatedYamlFile, TestEnvironment.AssentYamlConfiguration);
            }
        }
        
        [Test]
        public void CanPerformReplacementOnAGlobThatMatchesFilesInDifferentFormats()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, "values.*");
                Variables.AddFlag(ActionVariables.StructuredConfigurationFeatureFlag, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                
                var extractedPackageUpdatedJsonFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, JsonFileName));
                var extractedPackageUpdatedYamlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, YamlFileName));
                var extractedPackageUpdatedConfigFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, ConfigFileName));

                this.Assent(extractedPackageUpdatedJsonFile, TestEnvironment.AssentJsonDeepCompareConfiguration);
                this.Assent(extractedPackageUpdatedYamlFile, TestEnvironment.AssentYamlConfiguration);
                this.Assent(extractedPackageUpdatedConfigFile, TestEnvironment.AssentJsonDeepCompareConfiguration);
            }
        }
        
        [Test]
        public void FailsIfAnyFileMatchingAGlobFailsToParse()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, "*.json");
                Variables.AddFlag(ActionVariables.StructuredConfigurationFeatureFlag, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                
                result.AssertFailure();
                result.AssertErrorOutput("Unterminated string. Expected delimiter: \". Path '', line 3, position 1.");
            }
        }
        
        [Test]
        public void FailsIfAFileFailsToParseWhenThereAreManyGlobs()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, $"{JsonFileName}\n{MalformedFileName}");
                Variables.AddFlag(ActionVariables.StructuredConfigurationFeatureFlag, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();
                result.AssertErrorOutput("Newtonsoft.Json.JsonReaderException");
            }
        }

        [Test]
        public void SucceedsButWarnsIfTargetIsADirectory()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, ".");
                Variables.AddFlag(ActionVariables.StructuredConfigurationFeatureFlag, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                result.AssertOutputContains("Skipping structured variable replacement on '.' because it is a directory.");
                
                var unchangedJsonFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, JsonFileName));
                var unchangedYamlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, YamlFileName));

                this.Assent(unchangedJsonFile, TestEnvironment.AssentJsonDeepCompareConfiguration);
                this.Assent(unchangedYamlFile, TestEnvironment.AssentYamlConfiguration);
            }
        }
        
        [Test]
        public void FailsAndWarnsIfAFileCannotBeParsedWhenFeatureFlagIsSet()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, MalformedFileName);
                Variables.AddFlag(ActionVariables.StructuredConfigurationFeatureFlag, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();

                // Indicates the file couldn't be parsed.
                result.AssertErrorOutput("Newtonsoft.Json.JsonReaderException");
            }
        }

        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }
    }
}