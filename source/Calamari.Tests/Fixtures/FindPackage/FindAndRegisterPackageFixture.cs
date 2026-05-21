using System;
using System.IO;
using System.Security.Cryptography;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning;

namespace Calamari.Tests.Fixtures.FindPackage
{
    [TestFixture]
    public class FindAndRegisterPackageFixture : CalamariFixture
    {
        readonly static string tentacleHome = TestEnvironment.GetTestPath("temp", "FindAndRegisterPackage");
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

        CalamariResult FindAndRegisterPackage(string id, string version, string hash, VersionFormat versionFormat = VersionFormat.Semver)
        {
            return Invoke(Calamari()
                .Action("find-and-register-package")
                .Argument("packageId", id)
                .Argument("packageVersion", version)
                .Argument("taskId", "ServerTasks-12345")
                .Argument("packageVersionFormat", versionFormat)
                .Argument("packageHash", hash));
        }

        CalamariResult FindAndRegisterPackageExact(string id, string version, string hash, bool exactMatch)
        {
            return Invoke(Calamari()
                .Action("find-and-register-package")
                .Argument("packageId", id)
                .Argument("packageVersion", version)
                .Argument("taskId", "ServerTasks-12345")
                .Argument("packageHash", hash)
                .Argument("exactMatch", exactMatch));
        }

        [Test]
        public void ShouldFindAndRegisterPackageAlreadyUploaded()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(packageId, VersionFactory.CreateSemanticVersion(packageVersion), ".nupkg"));
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                var result = FindAndRegisterPackage(packageId, packageVersion, acmeWeb.Hash);

                result.AssertSuccess();
                result.AssertOutput("Package {0} {1} hash {2} has already been uploaded", packageId, packageVersion, acmeWeb.Hash);

                result.AssertCalamariFoundPackageServiceMessage(Is.True);
                result.AssertFoundPackageServiceMessage();

                // Verify package was registered with the journal
                result.AssertOutput("Registered package use/lock for {0} v{1} and task ServerTasks-12345", packageId, packageVersion);
            }
        }

        [Test]
        public void ShouldNotRegisterWhenPackageNotFound()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                // Don't copy the package to downloadPath, so it won't be found
                var result = FindAndRegisterPackage(packageId, packageVersion, acmeWeb.Hash);

                result.AssertSuccess();
                result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, packageVersion, acmeWeb.Hash);
            }
        }

        [Test]
        public void ShouldFailWhenTaskIdNotProvided()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(packageId, VersionFactory.CreateSemanticVersion(packageVersion), ".nupkg"));
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                // Don't provide taskId argument
                var result = Invoke(Calamari()
                    .Action("find-and-register-package")
                    .Argument("packageId", packageId)
                    .Argument("packageVersion", packageVersion)
                    .Argument("packageHash", acmeWeb.Hash));

                result.AssertFailure();
                result.AssertErrorOutput("No task ID was specified. Please pass --taskId YourTaskId");
            }
        }

        [Test]
        public void ShouldNotRegisterEarlierPackageVersions()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(packageId, VersionFactory.CreateSemanticVersion(packageVersion), ".nupkg"));
                File.Copy(acmeWeb.FilePath, destinationFilePath);

                // Request a different version (1.0.1) which doesn't exist
                using (var newAcmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, "1.0.1")))
                {
                    var result = FindAndRegisterPackage(packageId, "1.0.1", newAcmeWeb.Hash);

                    result.AssertSuccess();
                    result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, "1.0.1", newAcmeWeb.Hash);
                    result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", packageId);

                    // Should still log the found package service message for the earlier version
                    result.AssertFoundPackageServiceMessage();
                }
            }
        }

        [Test]
        public void ShouldFindNoEarlierPackageVersions()
        {
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageId, packageVersion)))
            {
                var result = FindAndRegisterPackage(packageId, packageVersion, acmeWeb.Hash);

                result.AssertSuccess();
                result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, packageVersion, acmeWeb.Hash);
                result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                result.AssertOutput("No earlier packages for {0} has been uploaded", packageId);
            }
        }

        [Test]
        public void ShouldFindNoEarlierMavenPackageVersions()
        {
            var result = FindAndRegisterPackage(mavenPackageId, packageVersion, mavenPackageHash);

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
                    var result = FindAndRegisterPackage(packageId, newpackageVersion, newAcmeWeb.Hash);

                    result.AssertSuccess();
                    result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId, newpackageVersion, newAcmeWeb.Hash);
                    result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                    result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", packageId);
                    result.AssertOutput("  - {0}: {1}", packageVersion, destinationFilePath);

                    result.AssertFoundPackageServiceMessage();
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
                    var result = FindAndRegisterPackageExact(packageId, newpackageVersion, newAcmeWeb.Hash, true);

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

            var result = FindAndRegisterPackage(mavenPackageId, newpackageVersion, mavenPackageHash, VersionFormat.Maven);

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
                    var result = FindAndRegisterPackage(packageId, newpackageVersion, newAcmeWeb.Hash);

                    result.AssertSuccess();
                    result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.", packageId,
                        newpackageVersion,
                        newAcmeWeb.Hash);
                    result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
                    result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", packageId);
                    result.AssertOutput("  - {0}: {1}", packageVersion, destinationFilePath);

                    result.AssertFoundPackageServiceMessage();
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

            var result = FindAndRegisterPackage(mavenPackageId, newpackageVersion, mavenPackageHash, VersionFormat.Maven);

            result.AssertSuccess();
            result.AssertOutput("Package {0} version {1} hash {2} has not been uploaded.",
                mavenPackageId,
                newpackageVersion,
                mavenPackageHash);
            result.AssertOutput("Finding earlier packages that have been uploaded to this Tentacle");
            result.AssertOutput("Found 1 earlier version of {0} on this Tentacle", mavenPackageId);
            result.AssertOutput("  - {0}: {1}", packageVersion, destinationFilePath);

            result.AssertFoundPackageServiceMessage();
            var foundPackage = result.CapturedOutput.FoundPackage;
            Assert.AreEqual(VersionFactory.CreateMavenVersion(packageVersion), foundPackage.Version);
            Assert.AreEqual(mavenPackageHash, foundPackage.Hash);
            Assert.AreEqual(destinationFilePath, foundPackage.RemotePath);
            Assert.AreEqual(".jar", foundPackage.FileExtension);
            Assert.AreEqual(mavenPackageId, foundPackage.PackageId);
        }

        [Test]
        public void ShouldFindMavenPackageAlreadyUploaded()
        {
            var destinationFilePath = Path.Combine(downloadPath, PackageName.ToCachedFileName(mavenPackageId, VersionFactory.CreateMavenVersion(packageVersion), ".jar"));
            File.Copy(mavenPackage, destinationFilePath);

            var result = FindAndRegisterPackage(mavenPackageId, packageVersion, mavenPackageHash, VersionFormat.Maven);

            result.AssertSuccess();
            result.AssertCalamariFoundPackageServiceMessage(Is.True,
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

            // Verify package was registered with the journal
            result.AssertOutput("Registered package use/lock for {0} v{1} and task ServerTasks-12345", mavenPackageId, packageVersion);
        }

        [Test]
        public void ShouldFailWhenNoPackageIdIsSpecified()
        {
            var result = FindAndRegisterPackage("", "1.0.0", "Hash");

            result.AssertFailure();
            result.AssertErrorOutput("No package ID was specified. Please pass --packageId YourPackage");
        }

        [Test]
        public void ShouldFailWhenNoPackageVersionIsSpecified()
        {
            var result = FindAndRegisterPackage("Calamari", "", "Hash");

            result.AssertFailure();
            result.AssertErrorOutput("No package version was specified. Please pass --packageVersion 1.0.0.0");
        }

        [Test]
        public void ShouldFailWhenInvalidPackageVersionIsSpecified()
        {
            var result = FindAndRegisterPackage("Calamari", "1.0.*", "Hash");

            result.AssertFailure();
            result.AssertErrorOutput("Package version '1.0.*' is not a valid Semver version string. Please pass --packageVersionFormat with a different version type.");
        }

        [Test]
        public void ShouldFailWhenNoPackageHashIsSpecified()
        {
            var result = FindAndRegisterPackage("Calamari", "1.0.0", "");

            result.AssertFailure();
            result.AssertErrorOutput("No package hash was specified. Please pass --packageHash YourPackageHash");
        }
    }
}
