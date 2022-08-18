using System.IO;
using Assent;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    public class DeployPackageWithJsonConfigurationFixture : DeployPackageFixture
    {
        const string ServiceName = "Acme.JsonFileOutput";
        const string ServiceVersion = "1.0.0";
        const string JsonFileName = "values.json";

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        [Test]
        public void ShouldReplaceJsonPropertiesFromVariables()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, ServiceVersion)))
            {
                Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.StructuredConfigurationVariables);
                Variables.Set(ActionVariables.StructuredConfigurationVariablesTargets, JsonFileName);
                Variables.Set("departments:0:employees:0:name", "Jane");
                Variables.Set("departments:0:employees:1:age", "40");
                Variables.Set("phone", "0123 456 789");
                Variables.Set("departments:0:snacks", "[{ \"name\": \"lollies\", \"amount\": 3 }, { \"name\": \"soda\", \"amount\": 24 }]");
                Variables.Set("Octopus", "true");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedJsonFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, JsonFileName));

                this.Assent(extractedPackageUpdatedJsonFile, AssentConfiguration.Json);
            }
        }

        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }
    }
}