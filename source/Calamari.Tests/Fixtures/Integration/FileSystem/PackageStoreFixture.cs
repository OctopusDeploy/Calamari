using System;
using System.IO;
using System.Linq;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octopus.Versioning;
using Octopus.Versioning.Metadata;

namespace Calamari.Tests.Fixtures.Integration.FileSystem
{
    [TestFixture]
    public class PackageStoreFixture
    {        
        static readonly string TentacleHome = TestEnvironment.GetTestPath("Fixtures", "FileSystem");
        static readonly string PackagePath = Path.Combine(TentacleHome, "Files");

        [SetUp]
        public void SetUp()
        {
            Environment.SetEnvironmentVariable("TentacleHome", TentacleHome);

            if (!Directory.Exists(PackagePath))
                Directory.CreateDirectory(PackagePath);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(PackagePath))
                Directory.Delete(PackagePath, true);
        }

        [Test]
        public void FiltersPackagesWithHigherVersion()
        {
            using (new TemporaryFile(CreatePackage("1.0.0.1")))
            using (new TemporaryFile(CreatePackage("1.0.0.2")))
            using (new TemporaryFile(CreatePackage("2.0.0.2")))
            {
                var store = new PackageStore(
                    new GenericPackageExtractorFactory().createStandardGenericPackageExtractor());

                var packages = store.GetNearestPackages(new PackageMetadata()
                {
                    PackageId = "Acme.Web",
                    Version = "1.1.1.1",
                    VersionFormat = VersionFormat.Semver,
                    PackageSearchPattern = "Acme.Web*"
                });

                CollectionAssert.AreEquivalent(packages.Select(c => c.Metadata.Version.ToString()),
                    new[] {"1.0.0.1", "1.0.0.2"});
            }
        }

        [Test]
        public void IgnoresInvalidFiles()
        {
            using (new TemporaryFile(CreatePackage("1.0.0.1")))
            using (new TemporaryFile(CreateEmptyFile("1.0.0.2")))
            {
                var store = new PackageStore(
                    new GenericPackageExtractorFactory().createStandardGenericPackageExtractor());

                var packages = store.GetNearestPackages(new PackageMetadata()
                {
                    PackageId = "Acme.Web",
                    Version = "1.1.1.1",
                    VersionFormat = VersionFormat.Semver,
                    PackageSearchPattern = "Acme.Web*"
                });

                CollectionAssert.AreEquivalent(packages.Select(c => c.Metadata.Version.ToString()), new[] {"1.0.0.1"});
            }
        }

        private string CreateEmptyFile(string version)
        {
            var destinationPath = Path.Combine(PackagePath, "Acme.Web.nupkg-12345678-1234-1234-1234-1234567890ab");
            File.WriteAllText(destinationPath, "FAKESTUFF");
            return destinationPath;
        }

        private string CreatePackage(string version)
        {
            var sourcePackage = PackageBuilder.BuildSamplePackage("Acme.Web", version, true);
            var destinationPath = Path.Combine(
                PackagePath,
                Path.GetFileName(sourcePackage) + "-12345678-1234-1234-1234-1234567890ab");

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            File.Move(sourcePackage, destinationPath);
            return destinationPath;
        }
    }
}