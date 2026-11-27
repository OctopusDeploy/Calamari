using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Calamari.Benchmarks.Support;
using Octopus.Calamari.ConsolidatedPackage;
using Octopus.Calamari.ConsolidatedPackage.Api;

namespace Calamari.Benchmarks
{
    /// <summary>
    /// Measures reading a consolidated Calamari package — the path Octopus Server takes when it unpacks a
    /// flavour for a deployment target.
    ///
    /// Motivation: an observation that extracting from the ConsolidatedPackage took roughly 6 seconds where
    /// roughly 500ms was expected. That was never confirmed or dismissed, because there was no benchmark to
    /// measure it against. These benchmarks are that measurement.
    ///
    /// Note that this path uses <see cref="System.IO.Compression.ZipArchive"/> from the BCL, NOT SharpCompress.
    /// A SharpCompress version change cannot move these numbers. See <see cref="PackageExtractionBenchmarks"/>
    /// for the SharpCompress-backed extractors.
    ///
    /// To measure against a real consolidated package instead of a synthetic one, set
    /// <c>CALAMARI_BENCHMARK_CONSOLIDATED_PACKAGE</c> to its path. The <c>FilesPerPlatform</c> and
    /// <c>PayloadBytes</c> parameters are then ignored.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 10, invocationCount: 1)]
    public class ConsolidatedPackageBenchmarks
    {
        public const string RealPackageEnvironmentVariable = "CALAMARI_BENCHMARK_CONSOLIDATED_PACKAGE";

        string packagePath = null!;
        string? temporaryDirectory;
        IConsolidatedPackageStreamProvider streamProvider = null!;
        IConsolidatedPackage package = null!;
        (string flavour, string platform)[] allFlavourPlatforms = null!;

        /// <summary>Files per flavour per platform. A real self-contained Calamari flavour is in the low hundreds.</summary>
        [Params(50, 200)]
        public int FilesPerPlatform { get; set; }

        /// <summary>
        /// Bytes per file. Kept small by default so the harness stays usable; raise it to trade setup time
        /// for a payload closer to real assembly sizes.
        /// </summary>
        [Params(4096)]
        public int PayloadBytes { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            var realPackage = Environment.GetEnvironmentVariable(RealPackageEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(realPackage))
            {
                if (!File.Exists(realPackage))
                    throw new FileNotFoundException($"{RealPackageEnvironmentVariable} is set but no file exists at that path.", realPackage);

                packagePath = realPackage;
                Console.WriteLine($"Using real consolidated package: {packagePath} ({new FileInfo(packagePath).Length / (1024 * 1024)} MB)");
            }
            else
            {
                temporaryDirectory = Path.Combine(Path.GetTempPath(), "Calamari.Benchmarks", Guid.NewGuid().ToString("n"));
                packagePath = Path.Combine(temporaryDirectory, "Calamari.Consolidated.zip");

                var built = SyntheticConsolidatedPackage.Build(packagePath, FilesPerPlatform, PayloadBytes);
                Console.WriteLine($"Synthetic consolidated package: {built.ArchiveEntryCount} archive entries, "
                                  + $"{built.IndexEntryCount} index entries, {built.SizeOnDiskBytes / (1024 * 1024)} MB on disk.");
            }

            streamProvider = new FileBasedStreamProvider(packagePath);
            package = new ConsolidatedPackageFactory().LoadFrom(streamProvider);

            allFlavourPlatforms = package.Index.GetAvailablePackages()
                                         .SelectMany(p => package.Index.GetPackage(p.package)
                                                                 .PlatformFiles.Keys
                                                                 .Select(platform => (p.package, platform)))
                                         .ToArray();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            if (temporaryDirectory != null && Directory.Exists(temporaryDirectory))
                Directory.Delete(temporaryDirectory, recursive: true);
        }

        /// <summary>
        /// Reading index.json alone. Establishes the floor: whatever a full extract costs, this much of it is
        /// just opening the archive and deserialising the index.
        /// </summary>
        [Benchmark]
        public int LoadIndex()
            => new ConsolidatedPackageFactory().LoadFrom(streamProvider).Index.GetAvailablePackages().Count();

        /// <summary>
        /// One flavour, one platform — the single unit of work Server actually asks for.
        /// </summary>
        [Benchmark]
        public long ExtractSingleFlavourPlatform()
        {
            var (flavour, platform) = allFlavourPlatforms[0];
            return Drain(package.ExtractCalamariPackage(flavour, platform));
        }

        /// <summary>
        /// Every flavour and platform in sequence. This is the shape that surfaces the per-call cost of
        /// re-opening the archive and rebuilding the full entry lookup, which is where a surprising
        /// multi-second total would come from.
        /// </summary>
        [Benchmark]
        public long ExtractAllFlavourPlatforms()
        {
            long total = 0;
            foreach (var (flavour, platform) in allFlavourPlatforms)
                total += Drain(package.ExtractCalamariPackage(flavour, platform));

            return total;
        }

        /// <summary>
        /// ExtractCalamariPackage yields streams that are disposed as the enumerator advances, so each one has
        /// to be consumed before MoveNext. Copying to <see cref="Stream.Null"/> measures decompression without
        /// also measuring the write side of the filesystem.
        /// </summary>
        static long Drain(System.Collections.Generic.IEnumerable<(string entryName, long size, Stream sourceStream)> entries)
        {
            long bytes = 0;
            foreach (var (_, size, sourceStream) in entries)
            {
                sourceStream.CopyTo(Stream.Null);
                bytes += size;
            }

            return bytes;
        }
    }
}
