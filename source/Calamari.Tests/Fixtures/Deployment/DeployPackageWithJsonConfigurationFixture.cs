using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Variables;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment.Packages
{
    public class DeployPackageWithJsonConfigurationFixture : DeployPackageFixture
    {
        private const string ServiceName = "Acme.JsonFileOutput";

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }
        
        [Test]
        public void ShouldReplaceJsonPropertiesFromVariables()
        {
            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, "1.0.0")))
            {
                Variables.Set(SpecialVariables.Package.JsonConfigurationVariablesEnabled, "true");
                Variables.Set("department:0:employees:0:name", "Jane");

                Variables.Set("ExpectedOutcome",
                    "{" +
                    "\"departments\": [" +
                        "{" +
                            "\"name\": \"Sales\"," +
                            "\"employees\": [" +
                            "{" +
                                "\"name\": \"Jane\"," +
                                "\"age\": 43," +
                            "}," +
                            "{" +
                                "\"name\": \"Billy\"," +
                                "\"age\": 65" +
                            "}" +
                        "}" +
                    "]," +
                    "\"phone\": \"0000 000 000\"" +
                    "}");
                
                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();
            }
        }
        
        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }
    }
}