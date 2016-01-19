using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Azure.Tests.Deployment.Azure
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Windows)]
    public class DeployAzureCloudServiceSansPackageExtractionFixture : CalamariFixture
    {
        CalamariResult result;
        ICalamariFileSystem fileSystem;
        string stagingDirectory;

        [TestFixtureSetUp]
        public void Deploy()
        {
            OctopusTestAzureSubscription.IgnoreIfCertificateNotInstalled();

            var nugetPackageFile = PackageBuilder.BuildSamplePackage("Octopus.Sample.AzureCloudService", "1.0.0");

            var variablesFile = Path.GetTempFileName(); 
            var variables = new VariableDictionary();
            OctopusTestAzureSubscription.PopulateVariables(variables);
            OctopusTestCloudService.PopulateVariables(variables);
            variables.Set(SpecialVariables.Action.Azure.Slot, "Staging");
            variables.Set(SpecialVariables.Action.Azure.SwapIfPossible, false.ToString());
            variables.Set(SpecialVariables.Action.Azure.UseCurrentInstanceCount, false.ToString());

            variables.Set(SpecialVariables.Action.Name, "AzureCloudService");
            variables.Set(SpecialVariables.Release.Number, "1.0.0");

            // Disable cspkg extraction
            variables.Set(SpecialVariables.Action.Azure.CloudServicePackageExtractionDisabled, true.ToString());

            fileSystem = new WindowsPhysicalFileSystem();
            stagingDirectory = Path.GetTempPath(); 
            variables.Set(SpecialVariables.Action.Azure.PackageExtractionPath, stagingDirectory);

            variables.Save(variablesFile);

            result = Invoke(
                Calamari()
                    .Action("deploy-azure-cloud-service")
                    .Argument("package", nugetPackageFile)
                    .Argument("variables", variablesFile));       
        }

        [TestFixtureTearDown]
        public void CleanUp()
        {
            if (!string.IsNullOrWhiteSpace(stagingDirectory))
                fileSystem.DeleteDirectory(stagingDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void ShouldReturnZero()
        {
           result.AssertZero(); 
        }
    }
}