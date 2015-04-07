using System;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.FindPackage
{
    [TestFixture]
    public class FindPackageFixture : CalamariFixture
    {
        readonly string downloadPath = Path.Combine(GetPackageDownloadFolder(), "Files");
        readonly string packageId = "Acme.Web";
        readonly string version = "1.0.0.0";
        readonly string newVersion = "1.0.0.1";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", GetPackageDownloadFolder());
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

        static string GetPackageDownloadFolder()
        {
            string currentDirectory = typeof(FindPackageFixture).Assembly.FullLocalPath();
            string targetFolder = "source\\";
            int index = currentDirectory.LastIndexOf(targetFolder, StringComparison.OrdinalIgnoreCase);
            string solutionRoot = currentDirectory.Substring(0, index + targetFolder.Length);

            var packageDirectory = Path.Combine(solutionRoot, "Calamari.Tests\\bin\\Fixtures\\FindPackage");

            return packageDirectory;
        }

        CalamariResult FindPackages(string packageId, string packageVersion, string packageHash)
        {
            return Invoke(Calamari()
                .Action("find-package")
                .Argument("packageId", packageId)
                .Argument("packageVersion", packageVersion)
                .Argument("packageHash", packageHash));
        }

        [Test]
        public void ShouldFindNoEarlierPackageVersions()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, version)))
            {
                var result = FindPackages(packageId, version, acmeWeb.Hash);

                result.AssertZero();
                result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, version, acmeWeb.Hash);
                result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                result.AssertOutput("No earlier packages for {0} has been uploaded", packageId);
            }
        }

        [Test]
        public void ShouldFindOneEarlierPackageVersion()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, version)))
            {
                var destinationFilePath = Path.Combine(downloadPath,
                    Path.GetFileName(acmeWeb.FilePath) + "-" + Guid.NewGuid());
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                using (var newAcmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, newVersion)))
                {
                    var result = FindPackages(packageId, newVersion, newAcmeWeb.Hash);

                    result.AssertZero();
                    result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, newVersion,
                        newAcmeWeb.Hash);
                    result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                    result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", packageId);
                    result.AssertOutput("  - {0}: {1}", version, destinationFilePath);
                    result.AssertOutput("##octopus[foundPackage id=\"QWNtZS5XZWI=\" version=\"MS4wLjAuMA==\"");
                }
            }
        }

        [Test]
        public void ShouldFindPackageAlreadyUploaded()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, version)))
            {
                var destinationFilePath = Path.Combine(downloadPath,
                    Path.GetFileName(acmeWeb.FilePath) + "-" + Guid.NewGuid());
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                var result = FindPackages(packageId, version, acmeWeb.Hash);

                result.AssertZero();
                result.AssertOutput("##octopus[calamari-found-package]");
                result.AssertOutput("Package {0} {1} hash {2} has already been uploaded", packageId, version,
                    acmeWeb.Hash);
                result.AssertOutput("##octopus[foundPackage id=\"QWNtZS5XZWI=\" version=\"MS4wLjAuMA==\"");
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
