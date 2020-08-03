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
        public void FailsAndWarnsIfAFileCannotBeParsedWhenFallbackFlagIsSet()	
        {	
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))	
            {	
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);	
                Variables.AddFlag(ActionVariables.StructuredConfigurationFallbackFlag, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, MalformedFileName);	
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);	
                result.AssertFailure();	

                result.AssertOutput("Syntax error when parsing the file as Json: Unexpected character encountered while parsing value: ^. Path '', line 0, position 0.");	
            }	
        }	

        [Test]	
        public void ShouldNotTreatYamlFileAsYamlWhenFallbackFlagIsSet()	
        {	
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))	
            {	
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);	
                Variables.AddFlag(ActionVariables.StructuredConfigurationFallbackFlag, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, YamlFileName);	
                Variables.Set("key", "new-value");	

                var result = DeployPackage(file.FilePath);	
                result.AssertFailure();	

                // Indicates we tried to parse yaml as JSON.	
                result.AssertOutput("Syntax error when parsing the file as Json: Unexpected character encountered while parsing value: k. Path '', line 0, position 0.");	
            }	
        }

        [Test]
        public void ShouldPerformReplacementInYamlIfFlagIsSet()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, YamlFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedYamlFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, YamlFileName));

                this.Assent(extractedPackageUpdatedYamlFile, TestEnvironment.AssentYamlConfiguration);
            }
        }

        [Test]
        public void JsonShouldBeTriedBeforeOtherFormatsWhenGuessingTheBestFormat()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, YamlFileName);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                result.AssertOutputMatches("Attempting structured variable replacement on file .*?values.yaml with formats: Json, Yaml.");

            }
        }

        [Test]
        public void SucceedsButWarnsIfNoFilesMatchTarget()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
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
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, ConfigFileName);
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
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                
                result.AssertFailure();
                result.AssertOutput("Unterminated string. Expected delimiter: \". Path '', line 3, position 1.");
            }
        }
        
        [Test]
        public void FailsIfAFileFailsToParseWhenThereAreManyGlobs()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, $"{JsonFileName}\n{MalformedFileName}");
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();
                result.AssertOutput("Syntax error when parsing the file as Json: Unexpected character encountered while parsing value: ^. Path '', line 0, position 0.");
            }
        }

        [Test]
        public void SucceedsButWarnsIfTargetIsADirectory()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(ActionVariables.StructuredConfigurationVariablesEnabled, true);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, ".");
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
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertFailure();

                result.AssertOutput("Syntax error when parsing the file as Json: Unexpected character encountered while parsing value: ^. Path '', line 0, position 0.");
            }
        }

        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }
    }
}