using System;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Integration.FileSystem;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Deployment.Packages;
using NUnit.Framework;
using Octopus.Versioning.Semver;

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

            if (Directory.Exists(PackagePath))
            {
                Directory.Delete(PackagePath, true);
            }

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
                     CreatePackageExtractor(),
                     CalamariPhysicalFileSystem.GetPhysicalFileSystem()
                    );

                var packages = store.GetNearestPackages("Acme.Web", new SemanticVersion(1, 1, 1, 1));

                CollectionAssert.AreEquivalent(new[] { "1.0.0.1", "1.0.0.2" }, packages.Select(c => c.Version.ToString()));
            }
        }

        [Test]
        public void OldFormatsAreIgnored()
        {
            using (new TemporaryFile(CreatePackage("0.5.0.1")))
            using (new TemporaryFile(CreatePackage("1.0.0.1", true)))
            {
                var store = new PackageStore(
                    CreatePackageExtractor(),
                    CalamariPhysicalFileSystem.GetPhysicalFileSystem()
                );

                var packages = store.GetNearestPackages("Acme.Web", new SemanticVersion(1, 1, 1, 1));

                CollectionAssert.AreEquivalent(new[] { "0.5.0.1" }, packages.Select(c => c.Version.ToString()));
            }
        }

        [Test]
        public void IgnoresInvalidFiles()
        {
            using (new TemporaryFile(CreatePackage("1.0.0.1")))
            using (new TemporaryFile(CreateEmptyFile("1.0.0.2")))
            {
                var store = new PackageStore(
                    CreatePackageExtractor(),
                    CalamariPhysicalFileSystem.GetPhysicalFileSystem()
                );

                var packages = store.GetNearestPackages("Acme.Web", new SemanticVersion("1.1.1.1"));

                CollectionAssert.AreEquivalent(new[] { "1.0.0.1" }, packages.Select(c => c.Version.ToString()));
            }
        }

        private string CreateEmptyFile(string version)
        {
            var destinationPath = Path.Combine(PackagePath, PackageName.ToCachedFileName("Acme.Web", new SemanticVersion(version), ".nupkg"));
            File.WriteAllText(destinationPath, "FAKESTUFF");
            return destinationPath;
        }

        private string CreatePackage(string version, bool oldCacheFormat = false)
        {
            var sourcePackage = PackageBuilder.BuildSamplePackage("Acme.Web", version, true);

            var destinationPath = Path.Combine(PackagePath, oldCacheFormat
                ? $"Acme.Web.{version}.nupkg-fd55edc5-9b36-414b-a2d0-4a2deeb6b2ec"
                : PackageName.ToCachedFileName("Acme.Web", new SemanticVersion(version), ".nupkg"));

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            File.Move(sourcePackage, destinationPath);
            return destinationPath;
        }

        ICombinedPackageExtractor CreatePackageExtractor()
        {
            var log = new InMemoryLog();
            var variables = new CalamariVariables();
            var commandLineRunner = new TestCommandLineRunner(log, variables);

            return new CombinedPackageExtractor(log, variables, commandLineRunner);
        }
    }
}