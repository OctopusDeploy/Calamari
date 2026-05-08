using System;
using System.IO;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
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
        readonly string packageVersion = "1.0.0";

        [OneTimeSetUp]
        public void TestFixtureSetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", tentacleHome);
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
    }
}
