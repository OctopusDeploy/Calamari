using System.IO;
using System.Text.RegularExpressions;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Fixtures.ScriptCS;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Azure.Tests.Deployment.Azure
{
    [TestFixture]
    public class DeployAzureWebFixture : CalamariFixture
    {
        ICalamariFileSystem fileSystem;
        string stagingDirectory;
        VariableDictionary variables;
        CalamariResult result;

        [TestFixtureSetUp]
        public void Deploy()
        {
            const string webAppName = "octodemo003-dev";

            OctopusTestAzureSubscription.IgnoreIfCertificateNotInstalled();

            variables = new VariableDictionary();
            OctopusTestAzureSubscription.PopulateVariables(variables);
            variables.Set(SpecialVariables.Action.Azure.WebAppName, webAppName);

            variables.Set("foo", "bar");
            // Enable file substitution and configure the target
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());
            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, "web.config");

            fileSystem = new WindowsPhysicalFileSystem();
            stagingDirectory = Path.GetTempPath(); 
            variables.Set(SpecialVariables.Action.Azure.PackageExtractionPath, stagingDirectory);

            result = DeployPackage("Acme.Web");
        }

        [TestFixtureTearDown]
        public void CleanUp()
        {
            if (!string.IsNullOrWhiteSpace(stagingDirectory))
                fileSystem.DeleteDirectory(stagingDirectory, FailureOptions.IgnoreFailure);
        }

        [PlatformTest(CompatablePlatform.Windows)]
        public void ShouldDeployPackage()
        {
            result.AssertZero();

        }

        [PlatformTest(CompatablePlatform.Windows)]
        public void ShouldPerformVariableSubstitution()
        {
           result.AssertOutput(
               new Regex(@"Performing variable substitution on '.*web\.config'")); 
        }

        CalamariResult DeployPackage(string packageName)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageName, "1.0.0")))
            {
                variables.Save(variablesFile.FilePath);

                return Invoke(Calamari()
                    .Action("deploy-azure-web")
                    .Argument("package", acmeWeb.FilePath)
                    .Argument("variables", variablesFile.FilePath));       
            }
        }
    }
}