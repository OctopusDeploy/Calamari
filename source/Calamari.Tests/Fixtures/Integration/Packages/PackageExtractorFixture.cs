using System;
using System.IO;
using Calamari.Integration.Packages;
using Calamari.Integration.Packages.NuGet;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class PackageExtractorFixture : CalamariFixture
    {
        private const string PackageId = "Acme.Core";
        private const string PackageVersion = "1.0.0-bugfix";


        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz")]
        [TestCase(typeof(TarPackageExtractor), "tar")]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2")]
        [TestCase(typeof(ZipPackageExtractor), "zip")]
        [TestCase(typeof(NupkgExtractor), "nupkg")]
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
        [TestCase(typeof(NupkgExtractor), "nupkg", false)]
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
            Assert.AreEqual(4, filesExtracted, "Mismatch in the number of files extracted"); //If you edit the nupkg file with Nuget Package Explorer it will add a _._ file to EmptyFolder and you'll get 5 here.
            Assert.AreEqual("Im in a package!", text.TrimEnd('\n'), "The contents of the extractd file do not match the expected value");
        }

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", true)]
        [TestCase(typeof(TarPackageExtractor), "tar", true)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", true)]
        [TestCase(typeof(ZipPackageExtractor), "zip", true)]
        [TestCase(typeof(NupkgExtractor), "nupkg", false)]
        //[TestCase(typeof(TarLzwPackageExtractor), "tar.xz")]
        public void ExtractCanHandleNestedPackage(Type extractorType, string extension, bool preservesTimestamp)
        {
            var fileName = GetFileName(extension);

            var extractor = (IPackageExtractor)Activator.CreateInstance(extractorType);
            var targetDir = GetTargetDir(extractorType, fileName);

            extractor.Extract(fileName, targetDir, true);
            var textFileName = Path.Combine(targetDir, "file-from-child-archive.txt");
            Assert.That(File.Exists(textFileName), Is.False, $"The file '{Path.GetFileName(textFileName)}' should not have been extracted.");
            var childArchiveName = Path.Combine(targetDir, "child-archive." + extension);
            Assert.That(File.Exists(childArchiveName), Is.True, $"Expected nested archive '{Path.GetFileName(childArchiveName)}' to have been extracted");
        }

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", true)]
        [TestCase(typeof(TarPackageExtractor), "tar", true)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", true)]
        [TestCase(typeof(ZipPackageExtractor), "zip", true)]
        [TestCase(typeof(NupkgExtractor), "nupkg", false)]
        //[TestCase(typeof(TarLzwPackageExtractor), "tar.xz")]
        public void ExtractCanHandleNestedFolders(Type extractorType, string extension, bool preservesTimestamp)
        {
            var fileName = GetFileName(extension);

            var extractor = (IPackageExtractor)Activator.CreateInstance(extractorType);
            var targetDir = GetTargetDir(extractorType, fileName);

            extractor.Extract(fileName, targetDir, true);
            var textFileName = Path.Combine(targetDir, "ChildFolder", "file-in-child-folder.txt");
            Assert.That(File.Exists(textFileName), Is.True, $"The file '{Path.GetFileName(textFileName)}' should have been extracted.");

            //nupkg should not ignore files in the package folder unless they are in package/services/metadata
            var packageFolderFileName = Path.Combine(targetDir, "package", "file-in-the-package-folder.txt");
            Assert.That(File.Exists(packageFolderFileName), Is.True, $"The file '{Path.GetFileName(textFileName)}' should have been extracted.");
        }

        [Test]
        [TestCase(typeof(NupkgExtractor), "nupkg", false)]
        public void NupkgExtractDoesNotExtractMetadata(Type extractorType, string extension, bool preservesTimestamp)
        {
            var fileName = GetFileName(extension);

            var extractor = (IPackageExtractor)Activator.CreateInstance(extractorType);
            var targetDir = GetTargetDir(extractorType, fileName);

            extractor.Extract(fileName, targetDir, true);
            var textFileName = Path.Combine(targetDir, "package", "services", "metadata", "core-properties", "8e89f0a759d94c1aaab0626891f7b81f.psmdcp");
            Assert.That(File.Exists(textFileName), Is.False, $"The file '{Path.GetFileName(textFileName)}' should not have been extracted.");
        }

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", true)]
        [TestCase(typeof(TarPackageExtractor), "tar", true)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", true)]
        [TestCase(typeof(ZipPackageExtractor), "zip", true)]
        [TestCase(typeof(NupkgExtractor), "nupkg", false)]
        //[TestCase(typeof(TarLzwPackageExtractor), "tar.xz")]
        public void ExtractCanHandleEmptyFolders(Type extractorType, string extension, bool preservesTimestamp)
        {
            var fileName = GetFileName(extension);

            var extractor = (IPackageExtractor)Activator.CreateInstance(extractorType);
            var targetDir = GetTargetDir(extractorType, fileName);

            extractor.Extract(fileName, targetDir, true);
            var folderName = Path.Combine(targetDir, "EmptyFolder");
            Assert.That(Directory.Exists(folderName), Is.True, $"The empty folder '{Path.GetFileName(folderName)}' should have been extracted.");
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
