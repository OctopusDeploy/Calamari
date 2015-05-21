using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.Windows)]
    public class DeployAzureCloudServiceFixture : CalamariFixture
    {
        CalamariResult result;

        [TestFixtureSetUp]
        public void Deploy()
        {
            const string azureSubscriptionId = "8affaa7d-3d74-427c-93c5-2d7f6a16e754";
            const string certificateThumbprint = "86B5C8E5553981FED961769B2DA3028C619596AC";
            const string cloudServiceName = "octopustestapp";
            const string storageAccountName = "octopusteststorage";

            // To avoid putting the certificate details in GitHub, we will assume it is stored in the CertificateStore 
            // of the local machine, and ignore the test if not.
            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var certificates = store.Certificates.Find(X509FindType.FindByThumbprint, certificateThumbprint, false);

            if (certificates.Count == 0)
                Assert.Ignore("Azure tests can only run if the expected certificate is present in the Certificate Store");

            var nugetPackageFile = PackageBuilder.BuildSamplePackage("Octopus.Sample.AzureCloudService", "1.0.0");

            var variablesFile = Path.GetTempFileName(); 
            var variables = new VariableDictionary();
            variables.Set(SpecialVariables.Action.Azure.CertificateBytes, Convert.ToBase64String(certificates[0].Export(X509ContentType.Pfx)));
            variables.Set(SpecialVariables.Action.Azure.CertificateThumbprint, certificateThumbprint);
            variables.Set(SpecialVariables.Action.Azure.SubscriptionId, azureSubscriptionId);
            variables.Set(SpecialVariables.Action.Azure.CloudServiceName, cloudServiceName);
            variables.Set(SpecialVariables.Action.Azure.StorageAccountName, storageAccountName);
            variables.Set(SpecialVariables.Action.Azure.Slot, "Staging");
            variables.Set(SpecialVariables.Action.Azure.SwapIfPossible, false.ToString());
            variables.Set(SpecialVariables.Action.Azure.UseCurrentInstanceCount, false.ToString());

            variables.Set(SpecialVariables.Action.Name, "AzureCouldService");
            variables.Set(SpecialVariables.Release.Number, "1.0.0");
            variables.Save(variablesFile);

            result = Invoke(
                Calamari()
                    .Action("deploy-azure-cloud-service")
                    .Argument("package", nugetPackageFile)
                    .Argument("variables", variablesFile));       
        }

        [Test]
        public void ShouldReturnZero()
        {
           result.AssertZero(); 
        }
    }
}