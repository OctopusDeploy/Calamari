using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Calamari.Benchmarks.Support;
using Calamari.Common.Features.Packages;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Benchmarks
{
    /// <summary>
    /// Measures Calamari's own package extractors, which are backed by SharpCompress.
    ///
    /// This is the half of extraction cost that a SharpCompress version bump can actually move. Run it before
    /// and after a bump and compare — that is the question left open by SF-1864, where a jump of thirteen
    /// minor versions went in with no throughput measurement on either side.
    ///
    /// Extraction writes to disk, so these are wall-clock measurements taken one invocation at a time rather
    /// than BenchmarkDotNet's usual tight loop. Expect them to be noisier than in-memory benchmarks, and
    /// compare runs on the same machine.
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 10, invocationCount: 1)]
    public class PackageExtractionBenchmarks
    {
        string workingDirectory = null!;
        string packagePath = null!;
        string extractionDirectory = null!;
        IPackageExtractor extractor = null!;

        [Params("zip", "tar", "tar.gz", "tar.bz2")]
        public string Format { get; set; } = "zip";

        [Params(200, 2000)]
        public int FileCount { get; set; }

        [Params(4096)]
        public int PayloadBytes { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            workingDirectory = Path.Combine(Path.GetTempPath(), "Calamari.Benchmarks", Guid.NewGuid().ToString("n"));
            packagePath = SyntheticArchive.Build(Path.Combine(workingDirectory, "packages"), Format, FileCount, PayloadBytes);
            extractor = CreateExtractor(Format);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            if (Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, recursive: true);
        }

        [IterationSetup]
        public void IterationSetup()
        {
            // A fresh target per iteration: extracting over an existing tree measures overwrite, not extraction.
            extractionDirectory = Path.Combine(workingDirectory, "extracted", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(extractionDirectory);
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            if (Directory.Exists(extractionDirectory))
                Directory.Delete(extractionDirectory, recursive: true);
        }

        [Benchmark]
        public int Extract() => extractor.Extract(packagePath, extractionDirectory);

        static IPackageExtractor CreateExtractor(string format)
            => format switch
               {
                   "zip" => new ZipPackageExtractor(ConsoleLog.Instance),
                   "tar" => new TarPackageExtractor(ConsoleLog.Instance),
                   "tar.gz" => new TarGzipPackageExtractor(ConsoleLog.Instance),
                   "tar.bz2" => new TarBzipPackageExtractor(ConsoleLog.Instance),
                   _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown archive format.")
               };
    }
}
