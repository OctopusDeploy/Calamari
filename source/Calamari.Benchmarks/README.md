# Calamari.Benchmarks

Wall-clock and allocation measurements for archive handling. It is a tool you run by hand when you need a
number; CI compiles this project as part of the solution build but never executes the benchmarks.

That is deliberate. These are wall-clock, disk-touching measurements taken on shared build agents across
several platforms, so any regression threshold would be either too loose to catch anything or flaky enough
that people mute it. There is also no stored baseline to compare against, so a CI run would print numbers
into a build log that nobody diffs. See "Keeping this from rotting" below for what is guarded instead.

It exists because the repo had no benchmark of any kind, which left two questions unanswerable:

1. An observation that extracting from the ConsolidatedPackage took roughly **6 seconds** where roughly
   **500ms** was expected. There was nothing to measure it against, so it was never confirmed or dismissed.
2. Whether a SharpCompress version change moves extraction cost. SF-1864 moved SharpCompress thirteen minor
   versions (0.37.2 → 0.49.1) with no throughput measurement on either side of the bump.

## Running

```bash
# everything (slow — the tar.bz2 cases dominate)
dotnet run -c Release --project source/Calamari.Benchmarks

# one suite
dotnet run -c Release --project source/Calamari.Benchmarks -- --filter '*ConsolidatedPackageBenchmarks*'
dotnet run -c Release --project source/Calamari.Benchmarks -- --filter '*PackageExtractionBenchmarks*'

# smoke test that the harness still works, without waiting for real measurements
dotnet run -c Release --project source/Calamari.Benchmarks -- --filter '*' --job Dry
```

Release configuration is required; BenchmarkDotNet refuses to run against an unoptimised build.

## The two suites measure different things

**`ConsolidatedPackageBenchmarks`** covers `ConsolidatedPackage.ExtractCalamariPackage` — the path Octopus
Server takes when unpacking a flavour.

This path uses `System.IO.Compression.ZipArchive` from the BCL. **It does not use SharpCompress**, so a
SharpCompress bump cannot move these numbers. If you are evaluating an upgrade, this suite is the control,
not the experiment.

**`PackageExtractionBenchmarks`** covers Calamari's own extractors (`ZipPackageExtractor`,
`TarGzipPackageExtractor`, `TarBzipPackageExtractor`, `TarPackageExtractor`), which *are* SharpCompress-backed.
This is the suite to run before and after a version change.

## Measuring against a real consolidated package

By default the consolidated-package suite generates a synthetic archive: same shape as the real one
(content-addressed entries, an `index.json`, most files shared across platforms), but with small
pseudo-random payloads rather than real assemblies. That makes it repeatable and quick, and it is enough to
compare runs against each other — but the absolute numbers are lower than production, because production
payloads are much larger.

To measure the real thing, point it at an actual consolidated package:

```bash
CALAMARI_BENCHMARK_CONSOLIDATED_PACKAGE=/path/to/Calamari.<hash>.zip \
  dotnet run -c Release --project source/Calamari.Benchmarks -- --filter '*ConsolidatedPackageBenchmarks*'
```

The `FilesPerPlatform` and `PayloadBytes` parameters are ignored when that variable is set.

## Keeping this from rotting

The solution build catches compile breakage, but nothing here is executed in CI, so the risk is that the
harness quietly stops working and we only find out during the next upgrade — exactly when nobody wants to be
debugging a benchmark.

The fragile part is `Support/`, which builds the fixtures the benchmarks measure. Those classes are covered
by ordinary tests in the existing suite:

- `Calamari.Tests` → `SyntheticArchiveFixture` — every format it produces is extractable by the matching
  Calamari extractor, entries are spread over a directory tree, and payloads don't compress away to nothing.
- `Calamari.ConsolidateCalamariPackages.Tests` → `SyntheticConsolidatedPackageTests` — the generated package
  is readable by the real `ConsolidatedPackageFactory`, every flavour and platform resolves its files, and
  shared files really are stored once and referenced many times.

Those projects **source-link** the `Support/` files with a `<Compile Include>` rather than taking a
ProjectReference on this project, so BenchmarkDotNet does not become a dependency of packages that ship to
test agents. That only works while `Support/` stays free of BenchmarkDotNet types — keep benchmark
attributes in the benchmark classes, not in the builders.

## Reading the results

These write to disk, so they run one invocation per iteration under `RunStrategy.Monitoring` rather than
BenchmarkDotNet's usual tight loop. Expect more variance than an in-memory benchmark, and only compare runs
taken on the same machine — cross-machine and CI-vs-laptop comparisons are not meaningful.

`ExtractAllFlavourPlatforms` is the one to watch for the 6-second question. It walks every flavour and
platform in the index, which is the shape that exposes per-call cost: `ExtractCalamariPackage` re-opens the
archive and rebuilds a dictionary over *every* entry on each call, so cost scales with total archive size
rather than with the number of files that call actually wants.
