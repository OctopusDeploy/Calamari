using System.IO;
using System.IO.Packaging;
using System.Linq;
using Assent;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Packages.NuGet;
using Calamari.Tests.Helpers;
using Calamari.Variables;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Packages
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
                Variables.AddFlag(SpecialVariables.Package.JsonConfigurationVariablesEnabled, true);
                Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesTargets, JsonFileName);
                Variables.Set("departments:0:employees:0:name", "Jane");
                Variables.Set("departments:0:employees:1:age", "40");
                Variables.Set("phone", "0123 456 789");
                Variables.Set("departments:0:snacks", "[{ \"name\": \"lollies\", \"amount\": 3 }, { \"name\": \"soda\", \"amount\": 24 }]");
                Variables.Set("Octopus", "true");

                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                var extractedPackageUpdatedJsonFile = File.ReadAllText(Path.Combine(StagingDirectory, ServiceName, ServiceVersion, JsonFileName));

                this.Assent(extractedPackageUpdatedJsonFile, TestEnvironment.AssentJsonDeepCompareConfiguration);
            }
        }
        
        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }
    }
}