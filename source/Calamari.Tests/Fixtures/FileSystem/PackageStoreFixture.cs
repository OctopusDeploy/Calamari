using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Tests.Helpers;
using Calamari.Tests.Fixtures.Deployment.Packages;
using NUnit.Framework;
using SemanticVersion = NuGet.SemanticVersion;

namespace Calamari.Tests.Fixtures.FileSystem
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
                var store = new PackageStore(new GenericPackageExtractor());

                var packages = store.GetNearestPackages("Acme.Web", new SemanticVersion(1, 1, 1, 1));

                CollectionAssert.AreEquivalent(packages.Select(c => c.Metadata.Version.ToString()), new[] { "1.0.0.1", "1.0.0.2" });
            }
        }

        [Test]
        public void IgnoresInvalidFiles()
        {
            using (new TemporaryFile(CreatePackage("1.0.0.1")))
            using (new TemporaryFile(CreateEmptyFile("1.0.0.2")))
            {
                var store = new PackageStore(new GenericPackageExtractor());

                var packages = store.GetNearestPackages("Acme.Web", new SemanticVersion(1, 1, 1, 1));

                CollectionAssert.AreEquivalent(packages.Select(c => c.Metadata.Version.ToString()), new[] { "1.0.0.1" });
            }
        }

        private string CreateEmptyFile(string version)
        {
            var destinationPath = Path.Combine(PackagePath, "Acme.Web.nupkg-x");
            File.WriteAllText(destinationPath, "FAKESTUFF");
            return destinationPath;
        }

        private string CreatePackage(string version)
        {
            var sourcePackage = PackageBuilder.BuildSamplePackage("Acme.Web", version, true);
            var destinationPath = Path.Combine(PackagePath, Path.GetFileName(sourcePackage) + "-x");

            if(File.Exists(destinationPath))
                File.Delete(destinationPath);

            File.Move(sourcePackage, destinationPath);
            return destinationPath;
        }
    }
}
