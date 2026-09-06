using System;
using System.IO;
using System.Linq;
using Calamari.Benchmarks.Support;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.ConsolidatedPackage;

namespace Calamari.ConsolidateCalamariPackages.Tests;

/// <summary>
/// Guards the consolidated-package builder used by Calamari.Benchmarks against rot.
///
/// The benchmark project is compiled by the solution build but never executed in CI, so nothing else proves
/// this builder still produces a package the real loader can read. A benchmark whose fixture no longer
/// resembles production would quietly report meaningless numbers.
///
/// The class is source-linked into this project rather than referenced; see the Compile item in the csproj.
/// </summary>
[TestFixture]
public class SyntheticConsolidatedPackageTests
{
    const int FilesPerPlatform = 10;
    const int PayloadBytes = 128;

    string temporaryDirectory = null!;
    SyntheticConsolidatedPackage.BuiltPackage built = null!;

    [SetUp]
    public void SetUp()
    {
        temporaryDirectory = Path.Combine(Path.GetTempPath(), "Calamari.Benchmarks.Tests", Guid.NewGuid().ToString("n"));
        built = SyntheticConsolidatedPackage.Build(Path.Combine(temporaryDirectory, "Calamari.Consolidated.zip"), FilesPerPlatform, PayloadBytes);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(temporaryDirectory))
            Directory.Delete(temporaryDirectory, recursive: true);
    }

    [Test]
    public void IsReadableByTheRealLoader()
    {
        var package = new ConsolidatedPackageFactory().LoadFrom(new FileBasedStreamProvider(built.Path));

        var availableFlavours = package.Index.GetAvailablePackages().Select(p => p.package).ToArray();

        availableFlavours.Should().BeEquivalentTo(SyntheticConsolidatedPackage.Flavours);
    }

    [Test]
    public void EveryFlavourAndPlatformExtractsTheExpectedFiles()
    {
        var package = new ConsolidatedPackageFactory().LoadFrom(new FileBasedStreamProvider(built.Path));

        foreach (var flavour in SyntheticConsolidatedPackage.Flavours)
        foreach (var platform in SyntheticConsolidatedPackage.Platforms)
        {
            var entries = Drain(package, flavour, platform);

            entries.Should().HaveCount(FilesPerPlatform, $"{flavour}/{platform} should resolve every indexed file");
            entries.Should().OnlyContain(e => e.bytesRead == PayloadBytes, "every payload should be readable in full");
            entries.Select(e => e.entryName).Should().OnlyHaveUniqueItems();
        }
    }

    [Test]
    public void SharedFilesAreStoredOnceAndReferencedByEveryPlatform()
    {
        // This is the property that makes the fixture resemble a real consolidated package: the archive holds
        // far fewer entries than the index references, because consolidation dedupes binary-identical files
        // across platforms. Lose it and the benchmark measures an archive several times too large.
        built.ArchiveEntryCount.Should()
             .BeLessThan(built.IndexEntryCount,
                         "shared files should be stored once and pointed at by every platform");

        var expectedIndexEntries = SyntheticConsolidatedPackage.Flavours.Length
                                   * SyntheticConsolidatedPackage.Platforms.Length
                                   * FilesPerPlatform;
        built.IndexEntryCount.Should().Be(expectedIndexEntries);
    }

    [Test]
    public void ProducesAnArchiveOnDisk()
    {
        File.Exists(built.Path).Should().BeTrue();
        built.SizeOnDiskBytes.Should().Be(new FileInfo(built.Path).Length);
        built.SizeOnDiskBytes.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// ExtractCalamariPackage disposes each stream as the enumerator advances, so every payload has to be
    /// read before MoveNext. Materialising the sequence first would close them all.
    /// </summary>
    static (string entryName, long bytesRead)[] Drain(Octopus.Calamari.ConsolidatedPackage.Api.IConsolidatedPackage package, string flavour, string platform)
    {
        var results = new System.Collections.Generic.List<(string, long)>();
        foreach (var (entryName, _, sourceStream) in package.ExtractCalamariPackage(flavour, platform))
        {
            using var buffer = new MemoryStream();
            sourceStream.CopyTo(buffer);
            results.Add((entryName, buffer.Length));
        }

        return results.ToArray();
    }
}
