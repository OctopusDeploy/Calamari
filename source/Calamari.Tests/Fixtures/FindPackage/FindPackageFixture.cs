using System;
using System.IO;
using System.Security.Cryptography;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.FindPackage
{
    [TestFixture]
    public class FindPackageFixture : CalamariFixture
    {
        readonly static string tentacleHome = TestEnvironment.GetTestPath("temp", "FindPackage");
        readonly static string downloadPath = Path.Combine(tentacleHome, "Files");
        readonly string packageId = "Acme.Web";
        readonly string mavenPackageId = "com.acme:web";
        readonly string packageVersion = "1.0.0";
        readonly string newpackageVersion = "1.0.1";

        private readonly string mavenPackage = TestEnvironment.GetTestPath("Java", "Fixtures", "Deployment", "Packages", "HelloWorld.0.0.1.jar");
        private string mavenPackageHash;

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", tentacleHome);

            using (var file = File.OpenRead(mavenPackage))
            {
                mavenPackageHash = BitConverter.ToString(SHA1.Create().ComputeHash(file)).Replace("-", "").ToLowerInvariant();
            }

        }

        [OneTimeTearDown]
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
            if (Directory.Exists(downloadPath))
                Directory.Delete(downloadPath, true);
        }

        CalamariResult FindPackages(string id, string version, string hash, VersionFormat versionFormat = VersionFormat.Semver)
        {
            return Invoke(Calamari()
                .Action("find-package")
                .Argument("packageId", id)
                .Argument("packageVersion", version)
                .Argument("packageVersionFormat", versionFormat)
                .Argument("packageHash", hash));
        }

        CalamariResult FindPackagesExact(string id, string version, string hash, bool exactMatch)
        {
            return Invoke(Calamari()
                .Action("find-package")
                .Argument("packageId", id)
                .Argument("packageVersion", version)
                .Argument("packageHash", hash)
                .Argument("exactMatch", exactMatch));
        }

        [Test]
        public void ShouldFindNoEarlierPackageVersions()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var result = FindPackages(packageId, packageVersion, acmeWeb.Hash);

                result.AssertSuccess();
                result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, packageVersion, acmeWeb.Hash);
                result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                result.AssertOutput("No earlier packages for {0} has been uploaded", packageId);
            }
        }

        [Test]
        public void ShouldFindNoEarlierMavenPackageVersions()
        {
            var result = FindPackages(mavenPackageId, packageVersion, mavenPackageHash);

            result.AssertSuccess();
            result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", 
                mavenPackageId,
                packageVersion, mavenPackageHash);
            result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
            result.AssertOutput("No earlier packages for {0} has been uploaded", mavenPackageId);
        }

        [Test]
        public void ShouldFindOneEarlierPackageVersion()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(packageId, VersionFactory.CreateSemanticVersion(packageVersion), ".nupkg"));
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                using (var newAcmeWeb =
                    new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, newpackageVersion)))
                {
                    var result = FindPackages(packageId, newpackageVersion, newAcmeWeb.Hash);

                    result.AssertSuccess();
                    result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.",  packageId, newpackageVersion, newAcmeWeb.Hash);
                    result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                    result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", packageId);
                    result.AssertOutput("  - {0}: {1}", packageVersion, destinationFilePath);

                    result.AssertServiceMessage(ServiceMessageNames.FoundPackage.Name, Is.True);
                    var foundPackage = result.CapturedOutput.FoundPackage;
                    Assert.AreEqual(VersionFactory.CreateSemanticVersion(packageVersion), foundPackage.Version);
                    Assert.AreEqual(acmeWeb.Hash, foundPackage.Hash);
                    Assert.AreEqual(destinationFilePath, foundPackage.RemotePath);
                    Assert.AreEqual(".nupkg", foundPackage.FileExtension);
                    Assert.AreEqual(packageId, foundPackage.PackageId);
                }
            }
        }

        [Test]
        public void ShouldNotFindEarlierPackageVersionWhenExactMatchRequested()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(packageId, VersionFactory.CreateSemanticVersion(packageVersion), ".nupkg"));
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                using (var newAcmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, newpackageVersion)))
                {
                    var result = FindPackagesExact(packageId, newpackageVersion, newAcmeWeb.Hash, true);

                    result.AssertSuccess();
                    result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, newpackageVersion,
                        newAcmeWeb.Hash);
                    result.AssertNoOutput("Finding earlier packages that have been uploaded to this Tentacle");
                }
            }
        }

        [Test]
        public void ShouldFindOneEarlierMavenPackageVersion()
        {
            var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(mavenPackageId, VersionFactory.CreateMavenVersion(packageVersion), ".jar"));
            File.Copy(mavenPackage, destinationFilePath);

            var result = FindPackages(mavenPackageId, newpackageVersion, mavenPackageHash, VersionFormat.Maven);

            result.AssertSuccess();
            result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.",
                mavenPackageId,
                newpackageVersion,
                mavenPackageHash);
            result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
            result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", mavenPackageId);
            result.AssertOutput("  - {0}: {1}", packageVersion, destinationFilePath);

            var foundPackage = result.CapturedOutput.FoundPackage;
            Assert.AreEqual(VersionFactory.CreateMavenVersion(packageVersion), foundPackage.Version);
            Assert.AreEqual(mavenPackageHash, foundPackage.Hash);
            Assert.AreEqual(destinationFilePath, foundPackage.RemotePath);
            Assert.AreEqual(".jar", foundPackage.FileExtension);
            Assert.AreEqual(mavenPackageId, foundPackage.PackageId);
        }

        [Test]
        public void ShouldFindTheCorrectPackageWhenSimilarPackageExist()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            using (var acmeWebTest = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId + ".Tests", packageVersion)))
            {
                var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(packageId, VersionFactory.CreateVersion(packageVersion, VersionFormat.Semver), ".nupkg"));
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                var destinationFilePathTest = Path.Combine(downloadPath, PackageName.ToCachedFileName(packageId + ".Tests", VersionFactory.CreateVersion(packageVersion, VersionFormat.Semver), ".nupkg"));
                File.Copy(acmeWebTest.FilePath, destinationFilePathTest);

                using (var newAcmeWeb =
                    new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, newpackageVersion)))
                {
                    var result = FindPackages(packageId, newpackageVersion, newAcmeWeb.Hash);

                    result.AssertSuccess();
                    result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId,
                        newpackageVersion,
                        newAcmeWeb.Hash);
                    result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                    result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", packageId);
                    result.AssertOutput("  - {0}: {1}", packageVersion, destinationFilePath);

                    result.AssertServiceMessage(ServiceMessageNames.FoundPackage.Name, Is.True);
                    var foundPackage = result.CapturedOutput.FoundPackage;
                    Assert.AreEqual(VersionFactory.CreateSemanticVersion(packageVersion), foundPackage.Version);
                    Assert.AreEqual(acmeWeb.Hash, foundPackage.Hash);
                    Assert.AreEqual(destinationFilePath, foundPackage.RemotePath);
                    Assert.AreEqual(".nupkg", foundPackage.FileExtension);
                    Assert.AreEqual(packageId, foundPackage.PackageId);   
                }
            }
        }

        [Test]
        public void ShouldFindTheCorrectMavenPackageWhenSimilarPackageExist()
        {
            var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(mavenPackageId, VersionFactory.CreateMavenVersion(packageVersion), ".jar"));
            File.Copy(mavenPackage, destinationFilePath);

            var destination2FilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(mavenPackageId + ".Test", VersionFactory.CreateMavenVersion(packageVersion), ".jar"));
            File.Copy(mavenPackage, destination2FilePath);

            var result = FindPackages(mavenPackageId, newpackageVersion, mavenPackageHash, VersionFormat.Maven);

            result.AssertSuccess();
            result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", 
                mavenPackageId,
                newpackageVersion,
                mavenPackageHash);
            result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
            result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", mavenPackageId);
            result.AssertOutput("  - {0}: {1}", packageVersion, destinationFilePath);

            result.AssertServiceMessage(ServiceMessageNames.FoundPackage.Name, Is.True);
            var foundPackage = result.CapturedOutput.FoundPackage;
            Assert.AreEqual(VersionFactory.CreateMavenVersion(packageVersion), foundPackage.Version);
            Assert.AreEqual(mavenPackageHash, foundPackage.Hash);
            Assert.AreEqual(destinationFilePath, foundPackage.RemotePath);
            Assert.AreEqual(".jar", foundPackage.FileExtension);
            Assert.AreEqual(mavenPackageId, foundPackage.PackageId);
        }

        [Test]
        public void ShouldFindPackageAlreadyUploaded()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(packageId, VersionFactory.CreateSemanticVersion(packageVersion), ".nupkg"));
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                var result = FindPackages(packageId, packageVersion, acmeWeb.Hash);

                result.AssertSuccess();
                result.AssertServiceMessage(
                    ServiceMessageNames.CalamariFoundPackage.Name,
                    Is.True,
                    message: "Expected service message '{0}' to be True",
                    args: ServiceMessageNames.CalamariFoundPackage.Name);

                result.AssertOutput("Package {0} {1} hash {2} has already been uploaded", packageId, packageVersion,
                    acmeWeb.Hash);

                result.AssertServiceMessage(ServiceMessageNames.FoundPackage.Name, Is.True);
                var foundPackage = result.CapturedOutput.FoundPackage;
                Assert.AreEqual(VersionFactory.CreateSemanticVersion(packageVersion), foundPackage.Version);
                Assert.AreEqual(acmeWeb.Hash, foundPackage.Hash);
                Assert.AreEqual(destinationFilePath, foundPackage.RemotePath);
                Assert.AreEqual(".nupkg", foundPackage.FileExtension);
                Assert.AreEqual(packageId, foundPackage.PackageId);
            }
        }

        [Test]
        public void ShouldFindMavenPackageAlreadyUploaded()
        {
            var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(mavenPackageId, VersionFactory.CreateMavenVersion(packageVersion), ".jar"));
            File.Copy(mavenPackage, destinationFilePath);

            var result = FindPackages(mavenPackageId, packageVersion, mavenPackageHash, VersionFormat.Maven);

            result.AssertSuccess();
            result.AssertServiceMessage(
                ServiceMessageNames.CalamariFoundPackage.Name,
                Is.True,
                message: "Expected service message '{0}' to be True",
                args: ServiceMessageNames.CalamariFoundPackage.Name);

            result.AssertOutput(
                "Package {0} {1} hash {2} has already been uploaded",
                mavenPackageId,
                packageVersion,
                mavenPackageHash);

            var foundPackage = result.CapturedOutput.FoundPackage;
            Assert.AreEqual(VersionFactory.CreateMavenVersion(packageVersion), foundPackage.Version);
            Assert.AreEqual(mavenPackageHash, foundPackage.Hash);
            Assert.AreEqual(destinationFilePath, foundPackage.RemotePath);
            Assert.AreEqual(".jar", foundPackage.FileExtension);
            Assert.AreEqual(mavenPackageId, foundPackage.PackageId);
        }

        [Test]
        public void ShouldFailWhenNoPackageIdIsSpecified()
        {
            var result = FindPackages("", "1.0.0", "Hash");

            result.AssertFailure();
            result.AssertErrorOutput("No package ID was specified. Please pass --packageId YourPackage");
        }

        [Test]
        public void ShouldFailWhenNoPackageVersionIsSpecified()
        {
            var result = FindPackages("Calamari", "", "Hash");

            result.AssertFailure();
            result.AssertErrorOutput("No package version was specified. Please pass --packageVersion 1.0.0.0");
        }

        [Test]
        public void ShouldFailWhenInvalidPackageVersionIsSpecified()
        {
            var result = FindPackages("Calamari", "1.0.*", "Hash");

            result.AssertFailure();
            result.AssertErrorOutput("Package version '1.0.*' is not a valid Semver version string. Please pass --packageVersionFormat with a different version type.");
        }

        [Test]
        public void ShouldFailWhenNoPackageHashIsSpecified()
        {
            var result = FindPackages("Calamari", "1.0.0", "");

            result.AssertFailure();
            result.AssertErrorOutput("No package hash was specified. Please pass --packageHash YourPackageHash");
        }
    }
}