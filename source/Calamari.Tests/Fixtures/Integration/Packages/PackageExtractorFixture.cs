using System;
using System.IO;
using Calamari.Integration.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class PackageExtractorFixture : CalamariFixture
    {
        private const string PackageId = "Acme.Core";
        private const string PackageVersion = "1.0.0.0-bugfix";


        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz")]
        [TestCase(typeof(TarPackageExtractor), "tar")]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2")]
        [TestCase(typeof(ZipPackageExtractor), "zip")]
        [TestCase(typeof(OpenPackagingConventionExtractor), "nupkg")]
        //[TestCase(typeof(TarLzwPackageExtractor), "tar.xz")]
        public void GetMetadataReturnsPackageDetails(Type extractorType, string extension)
        {
            var fileName = GetFileName(extension);
            var extractor = (IPackageExtractor) Activator.CreateInstance(extractorType);
            
            var metadata = extractor.GetMetadata(fileName);

            Assert.AreEqual(PackageId, metadata.Id);
            Assert.AreEqual(PackageVersion, metadata.Version);
        }

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", true)]
        [TestCase(typeof(TarPackageExtractor), "tar", true)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", true)]
        [TestCase(typeof(ZipPackageExtractor), "zip", true)]
        [TestCase(typeof(OpenPackagingConventionExtractor), "nupkg", false)]
        //[TestCase(typeof(TarLzwPackageExtractor), "tar.xz")]
        public void ExtractPumpsFilesToFilesystem(Type extractorType, string extension, bool preservesTimestamp)
        {
            var fileName = GetFileName(extension);
            var timeBeforeExtraction = DateTime.Now.AddSeconds(-1);

            var extractor = (IPackageExtractor)Activator.CreateInstance(extractorType);
            var targetDir = GetTargetDir(extractorType, fileName);

            var filesExtracted = extractor.Extract(fileName, targetDir, true);
            var textFileName = Path.Combine(targetDir, "my resource.txt");
            var text = File.ReadAllText(textFileName);
            var fileInfo = new FileInfo(textFileName);

            if (preservesTimestamp)
            {
                Assert.Less(fileInfo.LastWriteTime, timeBeforeExtraction);
            }
            Assert.AreEqual(1, filesExtracted);
            Assert.AreEqual("Im in a package!", text.TrimEnd('\n'));
        }

        private string GetFileName(string extension)
        {
            return GetFixtureResouce("Samples", string.Format("{0}.{1}.{2}", PackageId, PackageVersion, extension));
        }

        private string GetTargetDir(Type extractorType, string fileName)
        {
            var targetDir = Path.Combine(Path.GetDirectoryName(fileName), extractorType.Name);
            if (Directory.Exists(targetDir))
            {
                Directory.Delete(targetDir, true);
            }
            Directory.CreateDirectory(targetDir);
            return targetDir;
        }
    }
}
