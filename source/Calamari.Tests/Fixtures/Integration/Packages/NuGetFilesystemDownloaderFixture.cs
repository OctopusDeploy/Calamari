using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Integration.Packages.NuGet;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Util;
using Calamari.Tests.Helpers;
using Calamari.Util;
using NUnit.Framework;
using Octopus.Versioning.Semver;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class NuGetFileSystemDownloaderFixture : CalamariFixture
    {
        private string rootDir;
        [SetUp]
        public void Setup()
        {
            rootDir = GetFixtureResource(this.GetType().Name);
            TearDown();
            Directory.CreateDirectory(GetFixtureResource(rootDir));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(rootDir))
            {
                Directory.Delete(rootDir, true);
            }
        }

        [Test]
        public void FindsAndCopiesNugetPackageWithNugetFileFormat()
        {
            var originalPath = Path.Combine(rootDir, "Acme.Core.1.0.0.0-bugfix.nupkg");
            File.Copy(GetFixtureResource("Samples", "Acme.Core.1.0.0.0-bugfix.nupkg"), originalPath);
            var downloadPath = Path.Combine(rootDir, "DummyFile.nupkg");

            NuGetFileSystemDownloader.DownloadPackage("Acme.Core", new SemanticVersion("1.0.0.0-bugfix"), new Uri(rootDir), downloadPath);
            Assert.AreEqual(HashCalculator.Hash(originalPath), HashCalculator.Hash(downloadPath), "Expected source file to have been copied");
        }

        [Test]
        public void IgnoresZipPackages()
        {
            File.Copy(GetFixtureResource("Samples", "Acme.Core.1.0.0.0-bugfix.zip"), Path.Combine(rootDir, "Acme.Core.1.0.0.0-bugfix.zip"));
            var downloadPath = Path.Combine(rootDir, "DummyFile.nupkg");

            Assert.Throws<Exception>(() =>
                NuGetFileSystemDownloader.DownloadPackage("Acme.Core", new SemanticVersion("1.0.0.0-bugfix"), new Uri(rootDir), downloadPath)
            );
            FileAssert.DoesNotExist(downloadPath);
        }

        private string GetFileName()
        {
            return GetFixtureResource("Samples", "Acme.Core.1.0.0.0-bugfix.nupkg");
        }
    }
}
