using System;
using System.IO;
using System.Linq.Expressions;
using System.Text;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Azure;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using Calamari.Tests.Helpers.Azure;
using Microsoft.WindowsAzure;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.Windows)]
    public class UploadAzureCloudServicePackageConventionFixture
    {
        const string stagingDirectory = "C:\\Applications\\Foo"; 
        const string azureSubscriptionId = "8affaa7d-3d74-427c-93c5-2d7f6a16e754";
        const string certificateThumbprint = "86B5C8E5553981FED961769B2DA3028C619596AC";
        const string certificateBytes = "ThisIsNotAValidCertificate";
        const string storageAccountName = "AcmeStorage";
        ICalamariFileSystem fileSystem;
        IAzurePackageUploader packageUploader;
        VariableDictionary variables;
        RunningDeployment deployment;
        ISubscriptionCloudCredentialsFactory credentialsFactory;
        UploadAzureCloudServicePackageConvention convention;

        [SetUp]
        public void SetUp()
        {
            fileSystem = Substitute.For<ICalamariFileSystem>();
            packageUploader = Substitute.For<IAzurePackageUploader>();
            credentialsFactory = Substitute.For<ISubscriptionCloudCredentialsFactory>();
            credentialsFactory.GetCredentials(azureSubscriptionId, certificateThumbprint, certificateBytes)
                .Returns(new FakeSubscriptionCloudCredentials(azureSubscriptionId));

            variables = new VariableDictionary();
            variables.Set(SpecialVariables.OriginalPackageDirectoryPath, stagingDirectory);
            variables.Set(SpecialVariables.Action.Azure.SubscriptionId, azureSubscriptionId);
            variables.Set(SpecialVariables.Action.Azure.CertificateThumbprint, certificateThumbprint);
            variables.Set(SpecialVariables.Action.Azure.CertificateBytes, certificateBytes);
            variables.Set(SpecialVariables.Action.Azure.StorageAccountName, storageAccountName);
            deployment = new RunningDeployment(stagingDirectory, variables);

            convention = new UploadAzureCloudServicePackageConvention(fileSystem, packageUploader, credentialsFactory);
        }

        [Test]
        public void ShouldUploadPackage()
        {
            const string packageFileName = "Acme.cspkg";
            var packageFilePath = Path.Combine(stagingDirectory, packageFileName);
            variables.Set(SpecialVariables.Package.NuGetPackageVersion, "1.0.0");
            fileSystem.EnumerateFiles(stagingDirectory, "*.cspkg")
                .Returns(new[] { packageFilePath });
            fileSystem.OpenFile(packageFilePath, Arg.Any<FileMode>())
                .Returns(new MemoryStream(Encoding.UTF8.GetBytes("blah blah blah")));

            var uploadedUri = new Uri("http://azure.com/wherever/my-package.cspkg");

            packageUploader.Upload(
                Arg.Is<SubscriptionCloudCredentials>(cred => cred.SubscriptionId == azureSubscriptionId),
                storageAccountName, packageFilePath, Arg.Any<string>()) 
            .Returns(uploadedUri);

            convention.Install(deployment);

            Assert.AreEqual(uploadedUri.ToString(), variables.Get(SpecialVariables.Action.Azure.UploadedPackageUri));
        }
    }
}