using System;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using NUnit.Framework;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    /// <summary>
    /// Characterises how the package extractors behave on archives that are large or damaged, rather than on
    /// the small well-formed samples the rest of the suite uses.
    ///
    /// These are not regression tests for a known defect. They pin down behaviour that was previously
    /// unmeasured, so a future archive-library change that alters it shows up as a failing test rather than
    /// as a deployment failure. The vectors are the ones identified as uncovered during the SharpCompress
    /// 0.49.1 upgrade (SF-1864): entry counts far above the samples, deep directory nesting, long entry
    /// names, and archives that are truncated or corrupt rather than merely "not an archive at all".
    ///
    /// Two of these tests assert behaviour that is arguably wrong. They are written to the behaviour that
    /// exists today, and say so — see <see cref="TruncatedTarArchiveIsSilentlyExtractedInPart"/> and
    /// <see cref="CorruptedPayloadInUncompressedTarIsNotDetected"/>.
    /// </summary>
    [TestFixture]
    public class ArchiveRobustnessFixture
    {
        const int TruncationEntryCount = 50;
        const string EntryContent = "0123456789abcdef";

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", ArchiveType.Tar, CompressionType.GZip)]
        [TestCase(typeof(TarPackageExtractor), "tar", ArchiveType.Tar, CompressionType.None)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", ArchiveType.Tar, CompressionType.BZip2)]
        [TestCase(typeof(ZipPackageExtractor), "zip", ArchiveType.Zip, CompressionType.Deflate)]
        public void ExtractHandlesHighEntryCount(Type extractorType, string extension, ArchiveType archiveType, CompressionType compressionType)
        {
            // The sample packages have single-digit entry counts. A real deployment package routinely has
            // thousands, so this checks nothing degrades or truncates in between.
            const int entryCount = 1000;

            using var tempFolder = TemporaryDirectory.Create();
            var packageFile = Path.Combine(tempFolder.DirectoryPath, $"many-entries.{extension}");
            var extractionDir = CreateExtractionDirectory(tempFolder);

            WriteArchive(packageFile,
                         archiveType,
                         compressionType,
                         writer =>
                         {
                             for (var i = 0; i < entryCount; i++)
                                 writer.Write($"content/group{i % 16:d2}/file{i:d5}.txt", PayloadStream($"entry {i}"));
                         });

            var extractor = CreateExtractor(extractorType);

            var filesExtracted = extractor.Extract(packageFile, extractionDir);

            filesExtracted.Should().Be(entryCount);
            Directory.GetFiles(extractionDir, "*.txt", SearchOption.AllDirectories).Should().HaveCount(entryCount);
            File.ReadAllText(Path.Combine(extractionDir, "content", "group00", "file00000.txt")).Should().Be("entry 0");
        }

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", ArchiveType.Tar, CompressionType.GZip)]
        [TestCase(typeof(TarPackageExtractor), "tar", ArchiveType.Tar, CompressionType.None)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", ArchiveType.Tar, CompressionType.BZip2)]
        [TestCase(typeof(ZipPackageExtractor), "zip", ArchiveType.Zip, CompressionType.Deflate)]
        public void ExtractHandlesDeeplyNestedPaths(Type extractorType, string extension, ArchiveType archiveType, CompressionType compressionType)
        {
            // 40 single-character segments keeps the deepest path inside Windows' 260-character limit once the
            // temporary root is prepended, so this stays portable. It is still far deeper than any sample package.
            const int depth = 40;
            var nestedPath = string.Join("/", Enumerable.Repeat("d", depth));

            using var tempFolder = TemporaryDirectory.Create();
            var packageFile = Path.Combine(tempFolder.DirectoryPath, $"deep.{extension}");
            var extractionDir = CreateExtractionDirectory(tempFolder);

            WriteArchive(packageFile,
                         archiveType,
                         compressionType,
                         writer => writer.Write($"{nestedPath}/deep.txt", PayloadStream("deeply nested")));

            var extractor = CreateExtractor(extractorType);

            var filesExtracted = extractor.Extract(packageFile, extractionDir);

            filesExtracted.Should().Be(1);
            var expected = Path.Combine(new[] { extractionDir }.Concat(Enumerable.Repeat("d", depth)).Append("deep.txt").ToArray());
            File.Exists(expected).Should().BeTrue("the full nesting depth should be recreated on disk");
            File.ReadAllText(expected).Should().Be("deeply nested");
        }

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", ArchiveType.Tar, CompressionType.GZip)]
        [TestCase(typeof(TarPackageExtractor), "tar", ArchiveType.Tar, CompressionType.None)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", ArchiveType.Tar, CompressionType.BZip2)]
        [TestCase(typeof(ZipPackageExtractor), "zip", ArchiveType.Zip, CompressionType.Deflate)]
        public void ExtractHandlesLongEntryNames(Type extractorType, string extension, ArchiveType archiveType, CompressionType compressionType)
        {
            // 150 characters is longer than any real package entry while still leaving room for the temporary
            // root inside Windows' 260-character path limit. Behaviour *beyond* that limit is OS-divergent and
            // deliberately not asserted here; characterising it needs a Windows agent first.
            var longName = new string('a', 150) + ".txt";

            using var tempFolder = TemporaryDirectory.Create();
            var packageFile = Path.Combine(tempFolder.DirectoryPath, $"long-names.{extension}");
            var extractionDir = CreateExtractionDirectory(tempFolder);

            WriteArchive(packageFile, archiveType, compressionType, writer => writer.Write(longName, PayloadStream("long name")));

            var extractor = CreateExtractor(extractorType);

            var filesExtracted = extractor.Extract(packageFile, extractionDir);

            filesExtracted.Should().Be(1);
            File.ReadAllText(Path.Combine(extractionDir, longName)).Should().Be("long name");
        }

        [Test]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", ArchiveType.Tar, CompressionType.BZip2)]
        [TestCase(typeof(ZipPackageExtractor), "zip", ArchiveType.Zip, CompressionType.Deflate)]
        public void TruncatedArchiveThrows(Type extractorType, string extension, ArchiveType archiveType, CompressionType compressionType)
        {
            // Zip fails because truncation removes the central directory; bzip2 fails on its block checksums.
            using var tempFolder = TemporaryDirectory.Create();
            var packageFile = Path.Combine(tempFolder.DirectoryPath, $"truncated.{extension}");
            var extractionDir = CreateExtractionDirectory(tempFolder);

            WriteTruncatableArchive(packageFile, archiveType, compressionType);
            Truncate(packageFile);

            var extractor = CreateExtractor(extractorType);

            Assert.That(() => extractor.Extract(packageFile, extractionDir), Throws.Exception);
        }

        /// <summary>
        /// A truncated tar or tar.gz extracts whatever entries it can read and returns normally, reporting a
        /// file count lower than the archive actually contained.
        ///
        /// Neither format has a trailing index or whole-archive checksum, so the reader cannot tell a
        /// truncated archive from one that simply ended. The consequence is that a package which arrives
        /// incomplete deploys as a partial package with no error raised — Calamari has no independent record
        /// of how many entries it should have seen. Worth a fix, but that is a behaviour change beyond the
        /// scope of characterising it; this test exists so the change is visible when it happens.
        /// </summary>
        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", ArchiveType.Tar, CompressionType.GZip)]
        [TestCase(typeof(TarPackageExtractor), "tar", ArchiveType.Tar, CompressionType.None)]
        public void TruncatedTarArchiveIsSilentlyExtractedInPart(Type extractorType, string extension, ArchiveType archiveType, CompressionType compressionType)
        {
            using var tempFolder = TemporaryDirectory.Create();
            var packageFile = Path.Combine(tempFolder.DirectoryPath, $"truncated.{extension}");
            var extractionDir = CreateExtractionDirectory(tempFolder);

            WriteTruncatableArchive(packageFile, archiveType, compressionType);
            Truncate(packageFile);

            var extractor = CreateExtractor(extractorType);

            var filesExtracted = extractor.Extract(packageFile, extractionDir);

            filesExtracted.Should()
                          .BeGreaterThan(0)
                          .And.BeLessThan(TruncationEntryCount,
                                          "the archive was cut in half, so only some entries are readable");
            Directory.GetFiles(extractionDir, "*", SearchOption.AllDirectories)
                     .Should()
                     .HaveCount(filesExtracted, "the reported count should match what actually landed on disk");
        }

        [Test]
        [TestCase(typeof(TarGzipPackageExtractor), "tar.gz", ArchiveType.Tar, CompressionType.GZip)]
        [TestCase(typeof(TarBzipPackageExtractor), "tar.bz2", ArchiveType.Tar, CompressionType.BZip2)]
        [TestCase(typeof(ZipPackageExtractor), "zip", ArchiveType.Zip, CompressionType.Deflate)]
        public void CorruptedPayloadInCompressedArchiveThrows(Type extractorType, string extension, ArchiveType archiveType, CompressionType compressionType)
        {
            // Every compressed format detects the damage. Zip and gzip surface a ZlibException; bzip2
            // surfaces an InvalidFormatException on SharpCompress 0.49.1, where 0.37.2 leaked an
            // IndexOutOfRangeException from inside the decoder — the upgrade turned an implementation
            // artifact into a domain exception. The exact type is still an archive-library detail and is
            // deliberately not asserted; that it fails rather than writing garbage is what matters.
            using var tempFolder = TemporaryDirectory.Create();
            var packageFile = Path.Combine(tempFolder.DirectoryPath, $"corrupt.{extension}");
            var extractionDir = CreateExtractionDirectory(tempFolder);

            WriteSingleLargeEntryArchive(packageFile, archiveType, compressionType);
            CorruptEntryPayload(packageFile);

            var extractor = CreateExtractor(extractorType);

            Assert.That(() => extractor.Extract(packageFile, extractionDir), Throws.Exception);
        }

        /// <summary>
        /// An uncompressed tar carries no checksum over entry data, so a corrupted payload is written to disk
        /// verbatim and extraction reports success.
        ///
        /// This is inherent to the format rather than a defect in the extractor — there is nothing in a tar to
        /// check the bytes against. It is recorded here so the exposure is explicit: of the formats Calamari
        /// accepts, plain <c>.tar</c> is the one where silent content corruption is possible.
        /// </summary>
        [Test]
        public void CorruptedPayloadInUncompressedTarIsNotDetected()
        {
            using var tempFolder = TemporaryDirectory.Create();
            var packageFile = Path.Combine(tempFolder.DirectoryPath, "corrupt.tar");
            var extractionDir = CreateExtractionDirectory(tempFolder);

            WriteSingleLargeEntryArchive(packageFile, ArchiveType.Tar, CompressionType.None);
            CorruptEntryPayload(packageFile);

            var extractor = new TarPackageExtractor(ConsoleLog.Instance);

            var filesExtracted = extractor.Extract(packageFile, extractionDir);

            filesExtracted.Should().Be(1);
            File.ReadAllText(Path.Combine(extractionDir, "big.txt"))
                .Should()
                .NotBe(LargeEntryContent, "the corrupted bytes are written through without detection");
        }

        static string LargeEntryContent => new string(Enumerable.Range(0, 4096).Select(i => (char)('a' + i % 26)).ToArray());

        static IPackageExtractor CreateExtractor(Type extractorType)
            => (IPackageExtractor)Activator.CreateInstance(extractorType, ConsoleLog.Instance);

        static string CreateExtractionDirectory(TemporaryDirectory tempFolder)
        {
            var extractionDir = Path.Combine(tempFolder.DirectoryPath, "extraction");
            Directory.CreateDirectory(extractionDir);
            return extractionDir;
        }

        static void WriteTruncatableArchive(string packageFile, ArchiveType archiveType, CompressionType compressionType)
            => WriteArchive(packageFile,
                            archiveType,
                            compressionType,
                            writer =>
                            {
                                for (var i = 0; i < TruncationEntryCount; i++)
                                    writer.Write($"file{i:d3}.txt", PayloadStream(EntryContent));
                            });

        static void WriteSingleLargeEntryArchive(string packageFile, ArchiveType archiveType, CompressionType compressionType)
            => WriteArchive(packageFile, archiveType, compressionType, writer => writer.Write("big.txt", PayloadStream(LargeEntryContent)));

        static void WriteArchive(string packageFile, ArchiveType archiveType, CompressionType compressionType, Action<IWriter> write)
        {
            using var stream = File.OpenWrite(packageFile);
            using var writer = WriterFactory.OpenWriter(stream,
                                                  archiveType,
                                                  new WriterOptions(compressionType)
                                                  {
                                                      ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 }
                                                  });
            write(writer);
        }

        static MemoryStream PayloadStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

        static void Truncate(string path)
        {
            var bytes = File.ReadAllBytes(path);
            File.WriteAllBytes(path, bytes.Take(bytes.Length / 2).ToArray());
        }

        static void CorruptEntryPayload(string path)
        {
            var bytes = File.ReadAllBytes(path);

            // A 32-byte window a quarter of the way in. The archives this runs against hold a single large
            // entry, so that window is inside the entry payload: past the header, well before any trailer or
            // central directory. Damaging those instead would test archive structure, not content integrity.
            var start = bytes.Length / 4;
            for (var i = start; i < start + 32 && i < bytes.Length; i++)
                bytes[i] ^= 0xFF;

            File.WriteAllBytes(path, bytes);
        }
    }
}
