using System;
using System.IO;
using Calamari.Benchmarks.Support;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Packages
{
    /// <summary>
    /// Guards the archive builder used by Calamari.Benchmarks against rot.
    ///
    /// The benchmark project is compiled by the solution build but never executed in CI, so nothing else
    /// proves this builder still produces archives the extractors can actually read. Without that, a broken
    /// builder would only surface the next time someone needed a measurement — which is during an upgrade,
    /// when they are least inclined to debug the harness.
    ///
    /// The class is source-linked into this project rather than referenced; see the Compile item in
    /// Calamari.Tests.csproj for why.
    /// </summary>
    [TestFixture]
    public class SyntheticArchiveFixture
    {
        [Test]
        [TestCase("zip", typeof(ZipPackageExtractor))]
        [TestCase("tar", typeof(TarPackageExtractor))]
        [TestCase("tar.gz", typeof(TarGzipPackageExtractor))]
        [TestCase("tar.bz2", typeof(TarBzipPackageExtractor))]
        public void BuiltArchiveIsExtractableByTheMatchingExtractor(string format, Type extractorType)
        {
            const int fileCount = 20;
            const int payloadBytes = 256;

            using var tempFolder = TemporaryDirectory.Create();
            var packagePath = SyntheticArchive.Build(Path.Combine(tempFolder.DirectoryPath, "packages"), format, fileCount, payloadBytes);
            var extractionDir = Path.Combine(tempFolder.DirectoryPath, "extraction");
            Directory.CreateDirectory(extractionDir);

            new FileInfo(packagePath).Length.Should().BeGreaterThan(0);

            var extractor = (IPackageExtractor)Activator.CreateInstance(extractorType, ConsoleLog.Instance);

            var filesExtracted = extractor.Extract(packagePath, extractionDir);

            filesExtracted.Should()
                          .Be(fileCount, "the benchmark would otherwise be measuring a smaller archive than it reports");
            Directory.GetFiles(extractionDir, "*.bin", SearchOption.AllDirectories).Should().HaveCount(fileCount);
        }

        [Test]
        public void BuiltArchiveSpreadsEntriesAcrossDirectories()
        {
            // The benchmark is meant to include directory-creation cost, which only happens if entries are
            // actually nested rather than sitting in the archive root.
            using var tempFolder = TemporaryDirectory.Create();
            var packagePath = SyntheticArchive.Build(Path.Combine(tempFolder.DirectoryPath, "packages"), "zip", 32, 128);
            var extractionDir = Path.Combine(tempFolder.DirectoryPath, "extraction");
            Directory.CreateDirectory(extractionDir);

            new ZipPackageExtractor(ConsoleLog.Instance).Extract(packagePath, extractionDir);

            Directory.GetDirectories(extractionDir, "*", SearchOption.AllDirectories)
                     .Should()
                     .HaveCountGreaterThan(1, "entries should be spread over a directory tree, not flat");
        }

        [Test]
        public void PayloadsAreIncompressibleEnoughToBeWorthMeasuring()
        {
            // An all-zero or highly repetitive payload would deflate to almost nothing, and the benchmark
            // would measure bookkeeping rather than decompression. This is the property that keeps the
            // numbers meaningful, so it is worth pinning.
            const int fileCount = 20;
            const int payloadBytes = 4096;

            using var tempFolder = TemporaryDirectory.Create();
            var packagePath = SyntheticArchive.Build(Path.Combine(tempFolder.DirectoryPath, "packages"), "zip", fileCount, payloadBytes);

            var uncompressedTotal = (long)fileCount * payloadBytes;
            new FileInfo(packagePath).Length
                                     .Should()
                                     .BeGreaterThan((long)(uncompressedTotal * 0.5),
                                                    "random payloads should not compress away to nothing");
        }

        [Test]
        public void UnknownFormatIsRejected()
        {
            using var tempFolder = TemporaryDirectory.Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => SyntheticArchive.Build(tempFolder.DirectoryPath, "rar", 1, 16));
        }
    }
}
