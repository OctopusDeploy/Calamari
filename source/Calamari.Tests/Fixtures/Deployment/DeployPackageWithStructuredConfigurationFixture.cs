using System.IO;
using Assent;
using Calamari.Common.Features.StructuredVariables;
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

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }
        
        [Test]
        public void ShouldNotTreatYamlFileAsYamlUnlessFlagIsSet()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
                Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, YamlFileName);
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
                Variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
                Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, YamlFileName);
                Variables.AddFlag(StructuredConfigVariablesService.FeatureToggleVariableName, true);
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
                Variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
                Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, "doesnt-exist.json");
                Variables.AddFlag(StructuredConfigVariablesService.FeatureToggleVariableName, true);
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
                Variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
                Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, "values.config");
                Variables.AddFlag(StructuredConfigVariablesService.FeatureToggleVariableName, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                
                var extractedPackageUpdatedConfigFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, "values.config"));

                this.Assent(extractedPackageUpdatedConfigFile, TestEnvironment.AssentJsonDeepCompareConfiguration);
            }
        }
        
        [Test]
        public void CanPerformReplacementOnMultipleTargetFiles()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
                Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, $"{JsonFileName}\n{YamlFileName}");
                Variables.AddFlag(StructuredConfigVariablesService.FeatureToggleVariableName, true);
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
                Variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
                Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, "values.*");
                Variables.AddFlag(StructuredConfigVariablesService.FeatureToggleVariableName, true);
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
        public void SucceedsButWarnsIfTargetIsADirectory()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.AddFlag(PackageVariables.JsonConfigurationVariablesEnabled, true);
                Variables.Set(PackageVariables.JsonConfigurationVariablesTargets, ".");
                Variables.AddFlag(StructuredConfigVariablesService.FeatureToggleVariableName, true);
                Variables.Set("key", "new-value");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
                result.AssertOutputContains("Skipping structured variable replacement on '.' because it is a directory.");
            }
        }
        
        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }
    }
}