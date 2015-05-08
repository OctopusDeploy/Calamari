using System;
using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployAzureWebFixture : CalamariFixture
    {
        ICalamariFileSystem fileSystem;
        VariableDictionary variables;

        [SetUp]
        public void SetUp()
        {
            const string azureSubscriptionId = "8affaa7d-3d74-427c-93c5-2d7f6a16e754";
            const string webAppName = "octodemo003-dev";
            const string webSpaceName = "southeastasiawebspace";

            // To avoid putting the certificate in GitHub, we will store it in an environment variable
            // and ignore the test if the variable is not set.
            const string azureCertificateEnvironmentVariable = "OCTOPUS_TEST_AZURE_CERTIFICATE";
            var certificateBase64 = Environment.GetEnvironmentVariable(azureCertificateEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(certificateBase64))
                Assert.Ignore("Azure tests can only execute if environment-variable '{0}' is set.", azureCertificateEnvironmentVariable);

            variables = new VariableDictionary();
            variables.Set(SpecialVariables.Machine.Azure.SubscriptionId, azureSubscriptionId);
            variables.Set(SpecialVariables.Machine.Azure.CertificateBytes, certificateBase64);
            variables.Set(SpecialVariables.Machine.Azure.WebAppName, webAppName);
            variables.Set(SpecialVariables.Machine.Azure.WebSpaceName, webSpaceName);

            fileSystem = new WindowsPhysicalFileSystem();
        }

        [Test]
        public void ShouldDeployPackage()
        {
            var result = DeployPackage("Acme.Web");

            result.AssertZero();

            // Should remove staging directory
            Assert.False(fileSystem.DirectoryExists(result.CapturedOutput.OutputVariables[SpecialVariables.Package.Output.InstallationDirectoryPath]),
                "Staging directory should be deleted");
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