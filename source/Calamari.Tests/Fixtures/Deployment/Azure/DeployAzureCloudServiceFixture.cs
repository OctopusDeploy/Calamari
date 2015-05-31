using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment.Azure
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.Windows)]
    public class DeployAzureCloudServiceFixture : CalamariFixture
    {
        CalamariResult result;
        ICalamariFileSystem fileSystem;

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

            // Enable variable-substitution
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());
            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, "ServiceDefinition\\ServiceDefinition.csdef");

            variables.Save(variablesFile);

            result = Invoke(
                Calamari()
                    .Action("deploy-azure-cloud-service")
                    .Argument("package", nugetPackageFile)
                    .Argument("variables", variablesFile));       

            fileSystem = new WindowsPhysicalFileSystem();
        }

        [Test]
        public void ShouldReturnZero()
        {
           result.AssertZero(); 
        }

        [Test]
        public void ShouldRemoveStagingDirectory()
        {
            Assert.False(
                fileSystem.DirectoryExists(result.CapturedOutput.OutputVariables[SpecialVariables.Package.Output.InstallationDirectoryPath]));
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