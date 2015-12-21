using System.IO;
using System.Text.RegularExpressions;
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
    public class DeployAzureCloudServiceFixture : CalamariFixture
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
            variables.Set(SpecialVariables.Action.Azure.UseCurrentInstanceCount, true.ToString());

            variables.Set(SpecialVariables.Action.Name, "AzureCloudService");
            variables.Set(SpecialVariables.Release.Number, "1.0.0");

            // Enable variable-substitution
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());
            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, "ServiceDefinition\\ServiceDefinition.csdef");

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

        [Test]
        public void ShouldPerformVariableSubstitution()
        {
           result.AssertOutput(
               new Regex(@"Performing variable substitution on '.*ServiceDefinition\\ServiceDefinition\.csdef'")); 
        }

        [Test]
        public void ShouldRunPackagedScriptsWithAzureModulesAndSubscriptionAvailable()
        {
            // PostDeploy.ps1 should output the service-name
            result.AssertOutput("Service Name: " + OctopusTestCloudService.ServiceName);
        }
    }
}