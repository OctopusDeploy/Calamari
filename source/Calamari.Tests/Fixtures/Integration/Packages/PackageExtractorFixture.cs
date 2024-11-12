using System;
using System.IO;
using System.Text;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Packages.NuGet;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    [TestFixture]
    public class PackageExtractorFixture : CalamariFixture
    {
        private const string PackageId = "Acme.Core";
        private const string PackageVersion = "1.0.0.0-bugfix";

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", true)]
        [TestCase(typeof(TarPackageExtractor), "tar", true)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", true)]
        [TestCase(typeof(ZipPackageExtractor), "zip", true)]
        [TestCase(typeof(NupkgExtractor), "nupkg", false)]
        public void ExtractPumpsFilesToFilesystem(Type extractorType, string extension, bool preservesTimestamp)
        {
            var fileName = GetFileName(extension);
            var timeBeforeExtraction = DateTime.Now.AddSeconds(-1);

            var extractor = (IPackageExtractor) Activator.CreateInstance(extractorType, ConsoleLog.Instance);
            var targetDir = GetTargetDir(extractorType, fileName);

            var filesExtracted = extractor.Extract(fileName, targetDir);
            var textFileName = Path.Combine(targetDir, "my resource.txt");
            var text = File.ReadAllText(textFileName);
            var fileInfo = new FileInfo(textFileName);

            if (preservesTimestamp)
            {
                Assert.Less(fileInfo.LastWriteTime, timeBeforeExtraction);
            }

            Assert.AreEqual(9, filesExtracted, "Mismatch in the number of files extracted"); //If you edit the nupkg file with Nuget Package Explorer it will add a _._ file to EmptyFolder and you'll get 5 here.
            Assert.AreEqual("Im in a package!", text.TrimEnd('\n'), "The contents of the extractd file do not match the expected value");
        }
        
        
        [Test]
        public void CanExtractZipFileContainingSpecialCharacters()
        {
            var fileName = GetFixtureResource("Samples", string.Format("utf8.Filenames-1.0.0.zip"));

            Type extractorType = typeof(ZipPackageExtractor);
            
            var extractor = (IPackageExtractor) Activator.CreateInstance(extractorType, ConsoleLog.Instance);
            var targetDir = GetTargetDir(extractorType, fileName);

            var filesExtracted = extractor.Extract(fileName, targetDir);
            var textFileName = Path.Combine(targetDir, "á_ó_í_çø.txt");
            var text = File.ReadAllText(textFileName);
            

            Assert.AreEqual(1, filesExtracted, "Mismatch in the number of files extracted");
            Assert.AreEqual("Im in a package!", text.TrimEnd('\n'), "The contents of the extracted file do not match the expected value");
        }
        

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", true)]
        [TestCase(typeof(TarPackageExtractor), "tar", true)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", true)]
        [TestCase(typeof(ZipPackageExtractor), "zip", true)]
        [TestCase(typeof(NupkgExtractor), "nupkg", false)]
        public void ExtractCanHandleNestedPackage(Type extractorType, string extension, bool preservesTimestamp)
        {
            var fileName = GetFileName(extension);

            var extractor = (IPackageExtractor) Activator.CreateInstance(extractorType, ConsoleLog.Instance);
            var targetDir = GetTargetDir(extractorType, fileName);

            extractor.Extract(fileName, targetDir);
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
        public void ExtractCanHandleNestedFolders(Type extractorType, string extension, bool preservesTimestamp)
        {
            var fileName = GetFileName(extension);

            var extractor = (IPackageExtractor) Activator.CreateInstance(extractorType, ConsoleLog.Instance);
            var targetDir = GetTargetDir(extractorType, fileName);

            extractor.Extract(fileName, targetDir);
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

            var extractor = (IPackageExtractor) Activator.CreateInstance(extractorType, ConsoleLog.Instance);
            var targetDir = GetTargetDir(extractorType, fileName);

            extractor.Extract(fileName, targetDir);
            var textFileName = Path.Combine(targetDir, "package", "services", "metadata", "core-properties", "8e89f0a759d94c1aaab0626891f7b81f.psmdcp");
            Assert.That(File.Exists(textFileName), Is.False, $"The file '{Path.GetFileName(textFileName)}' should not have been extracted.");
        }

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", true)]
        [TestCase(typeof(TarPackageExtractor), "tar", true)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", true)]
        [TestCase(typeof(ZipPackageExtractor), "zip", true)]
        [TestCase(typeof(NupkgExtractor), "nupkg", false)]
        public void ExtractCanHandleEmptyFolders(Type extractorType, string extension, bool preservesTimestamp)
        {
            var fileName = GetFileName(extension);

            var extractor = (IPackageExtractor) Activator.CreateInstance(extractorType, ConsoleLog.Instance);
            var targetDir = GetTargetDir(extractorType, fileName);

            extractor.Extract(fileName, targetDir);
            var folderName = Path.Combine(targetDir, "EmptyFolder");
            Assert.That(Directory.Exists(folderName), Is.True, $"The empty folder '{Path.GetFileName(folderName)}' should have been extracted.");
        }

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", ArchiveType.Tar, CompressionType.GZip)]
        [TestCase(typeof(TarPackageExtractor), "tar", ArchiveType.Tar, CompressionType.None)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", ArchiveType.Tar, CompressionType.BZip2)]
        [TestCase(typeof(ZipPackageExtractor), "zip", ArchiveType.Zip, CompressionType.Deflate)]
        public void ExtractTakesIntoAccountEncoding(Type extractorType, string extension, ArchiveType archiveType, CompressionType compressionType)
        {
            const string characterSetToTest = "âçÿú¢ŤṵﻝﺕﻻⱩῠᾌ";

            var memoryStream = new MemoryStream();
            memoryStream.WriteByte(1);

            using (var tempFolder = TemporaryDirectory.Create())
            {
                var fileName = Path.Combine(tempFolder.DirectoryPath, $"package.{extension}");
                using (Stream stream = File.OpenWrite(fileName))
                using (var writer = WriterFactory.Open(stream, archiveType, new WriterOptions(compressionType)
                {
                    ArchiveEncoding = new ArchiveEncoding {Default = Encoding.UTF8}
                }))
                {
                    foreach (var c in characterSetToTest)
                    {
                        memoryStream.Position = 0;
                        writer.Write($"{c}", memoryStream);
                    }
                }

                var extractor = (IPackageExtractor) Activator.CreateInstance(extractorType, ConsoleLog.Instance);
                var targetDir = GetTargetDir(extractorType, fileName);

                extractor.Extract(fileName, targetDir);

                foreach (var c in characterSetToTest)
                {
                    File.Exists(Path.Combine(targetDir, c.ToString())).Should().BeTrue();
                }
            }
        }

        [Test]
        //Latest version of SharpCompress throws an exception if a symbolic link is encountered and we haven't provided a handler for it.
        public void ExtractIgnoresSymbolicLinks()
        {
            var log = new InMemoryLog();
            var fileName = GetFixtureResource("Samples", string.Format("{0}.{1}.{2}", PackageId, "1.0.0.0-symlink", "tar.gz"));

            var extractor = new TarGzipPackageExtractor(log);
            var targetDir = GetTargetDir(typeof(TarGzipPackageExtractor), fileName);

                extractor.Extract(fileName, targetDir);

            //If we get this far and an exception hasn't been thrown, the test has served it's purpose'

            //If the symbolic link actually exists, someone has implimented it but hasn't updated/deleted this test
            var symlink = Path.Combine(targetDir, "octopus-sample", "link-to-sample");
            Assert.That(File.Exists(symlink), Is.False, $"Symbolic link exists, please update this test.");

            log.StandardOut.Should().ContainMatch("Cannot create symbolic link*");
        }

        private string GetFileName(string extension)
        {
            return GetFixtureResource("Samples", string.Format("{0}.{1}.{2}", PackageId, PackageVersion, extension));
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