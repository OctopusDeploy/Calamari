using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.ServiceMessages;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.FindPackage
{
    [TestFixture]
    [Category(TestEnvironment.CompatableOS.All)]
    public class FindPackageFixture : CalamariFixture
    {
        readonly static string tentacleHome = TestEnvironment.GetTestPath("temp", "FindPackage");
        readonly static string downloadPath = Path.Combine(tentacleHome, "Files");
        readonly string packageId = "Acme.Web";
        readonly string packageVersion = "1.0.0.0";
        readonly string newpackageVersion = "1.0.0.1";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", tentacleHome);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            Environment.SetEnvironmentVariable("TentacleHome", null);
        }

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(downloadPath))
                Directory.CreateDirectory(downloadPath);
        }

        [TearDown]
        public void TearDown()
        {
            if(Directory.Exists(downloadPath))
                Directory.Delete(downloadPath, true);
        }

        CalamariResult FindPackages(string id, string version, string hash)
        {
            return Invoke(Calamari()
                .Action("find-package")
                .Argument("packageId", id)
                .Argument("packageVersion", version)
                .Argument("packageHash", hash));
        }

        [Test]
        public void ShouldFindNoEarlierPackageVersions()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var result = FindPackages(packageId, packageVersion, acmeWeb.Hash);

                result.AssertZero();
                result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, packageVersion, acmeWeb.Hash);
                result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                result.AssertOutput("No earlier packages for {0} has been uploaded", packageId);
            }
        }

        [Test]
        public void ShouldFindOneEarlierPackageVersion()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var destinationFilePath = Path.Combine(downloadPath,
                    Path.GetFileName(acmeWeb.FilePath) + "-" + Guid.NewGuid());
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                using (var newAcmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, newpackageVersion)))
                {
                    var result = FindPackages(packageId, newpackageVersion, newAcmeWeb.Hash);

                    result.AssertZero();
                    result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, newpackageVersion,
                        newAcmeWeb.Hash);
                    result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                    result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", packageId);
                    result.AssertOutput("  - {0}: {1}", packageVersion, destinationFilePath);

                    result.AssertServiceMessage(ServiceMessageNames.FoundPackage.Name, Is.True,
                        new Dictionary<string, object>
                    {
                        {"Metadata.Id", packageId},
                        {"Metadata.Version", packageVersion},
                        {"Metadata.Hash", acmeWeb.Hash},
                        {"FullPath", destinationFilePath}
                    });
                }
            }
        }

        [Test]
        public void ShouldFindPackageAlreadyUploaded()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var destinationFilePath = Path.Combine(downloadPath,
                    Path.GetFileName(acmeWeb.FilePath) + "-" + Guid.NewGuid());
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                var result = FindPackages(packageId, packageVersion, acmeWeb.Hash);

                result.AssertZero();
                result.AssertServiceMessage(
                    ServiceMessageNames.CalamariFoundPackage.Name, 
                    Is.True,
                    message: "Expected service message '{0}' to be True", 
                    args: ServiceMessageNames.CalamariFoundPackage.Name);

                result.AssertOutput("Package {0} {1} hash {2} has already been uploaded", packageId, packageVersion,
                    acmeWeb.Hash);
                result.AssertServiceMessage(ServiceMessageNames.FoundPackage.Name, Is.True,
                    new Dictionary<string, object>
                    {
                        {"Metadata.Id", packageId},
                        {"Metadata.Version", packageVersion},
                        {"Metadata.Hash", acmeWeb.Hash},
                        {"FullPath", destinationFilePath}
                    });
            }
        }

        [Test]
        public void ShouldFailWhenNoPackageIdIsSpecified()
        {
            var result = FindPackages("", "1.0.0.0", "Hash");

            result.AssertNonZero();
            result.AssertErrorOutput("No package ID was specified. Please pass --packageId YourPackage");
        }

        [Test]
        public void ShouldFailWhenNoPackageVersionIsSpecified()
        {
            var result = FindPackages("Calamari", "", "Hash");

            result.AssertNonZero();
            result.AssertErrorOutput("No package version was specified. Please pass --packageVersion 1.0.0.0");
        }

        [Test]
        public void ShouldFailWhenInvalidPackageVersionIsSpecified()
        {
            var result = FindPackages("Calamari", "1.0.0.*", "Hash");

            result.AssertNonZero();
            result.AssertErrorOutput("Package version '1.0.0.*' is not a valid Semantic Version");
        }

        [Test]
        public void ShouldFailWhenNoPackageHashIsSpecified()
        {
            var result = FindPackages("Calamari", "1.0.0.0", "");

            result.AssertNonZero();
            result.AssertErrorOutput("No package hash was specified. Please pass --packageHash YourPackageHash");
        }

    }
}
