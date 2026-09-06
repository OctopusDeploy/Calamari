using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Octopus.Calamari.ConsolidatedPackage;
using Octopus.Calamari.ConsolidatedPackage.Api;

namespace Calamari.Benchmarks.Support
{
    /// <summary>
    /// Builds a zip shaped like a real consolidated Calamari package: content-addressed entries
    /// (<c>&lt;hash&gt;/&lt;filename&gt;</c>) plus an <c>index.json</c> mapping each flavour and platform
    /// onto those entries.
    ///
    /// The shape matters more than the bytes. Two properties of the real package drive extraction cost and
    /// are reproduced here:
    /// <list type="bullet">
    /// <item>Most files are byte-identical across platforms of a flavour, so consolidation stores one entry
    /// and the index points many platforms at it. That is what makes the archive entry count much smaller
    /// than the index entry count.</item>
    /// <item>Every <c>ExtractCalamariPackage</c> call re-opens the archive and rebuilds a lookup over
    /// <em>all</em> entries, so cost per call scales with total archive size, not with the number of files
    /// that call actually wants.</item>
    /// </list>
    /// </summary>
    public static class SyntheticConsolidatedPackage
    {
        /// <summary>Flavours in a real consolidated package, as of the build at time of writing.</summary>
        public static readonly string[] Flavours =
        {
            "Calamari",
            "Calamari.AzureAppService",
            "Calamari.AzureResourceGroup",
            "Calamari.AzureScripting",
            "Calamari.AzureServiceFabric",
            "Calamari.AzureWebApp",
            "Calamari.GoogleCloudScripting",
            "Calamari.Scripting",
            "Calamari.Terraform"
        };

        public static readonly string[] Platforms =
        {
            "win-x64",
            "linux-x64",
            "linux-arm",
            "linux-arm64",
            "osx-x64",
            "osx-arm64"
        };

        /// <summary>
        /// Fraction of a flavour's files that are byte-identical across every platform, and therefore
        /// stored once. The remainder are platform-specific (native binaries, the RID-stamped nuspec).
        /// </summary>
        const double SharedFileFraction = 0.7;

        public static BuiltPackage Build(string destinationPath, int filesPerPlatform, int payloadBytes)
        {
            var entryContents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var packages = new Dictionary<string, IConsolidatedPackageIndex.Package>(StringComparer.OrdinalIgnoreCase);

            var sharedCount = (int)(filesPerPlatform * SharedFileFraction);
            var platformSpecificCount = filesPerPlatform - sharedCount;

            foreach (var flavour in Flavours)
            {
                var platformFiles = new Dictionary<string, IConsolidatedPackageIndex.FileTransfer[]>(StringComparer.Ordinal);

                // Shared entries are created once per flavour and referenced by every platform, mirroring
                // how consolidation dedupes binary-identical managed assemblies.
                var sharedEntries = Enumerable.Range(0, sharedCount)
                                              .Select(i =>
                                                      {
                                                          var destination = $"{flavour}.Shared{i}.dll";
                                                          var source = ContentAddressedEntryName(flavour, "shared", i, destination);
                                                          entryContents[source] = Payload(source, payloadBytes);
                                                          return new IConsolidatedPackageIndex.FileTransfer(source, destination);
                                                      })
                                              .ToArray();

                foreach (var platform in Platforms)
                {
                    var platformSpecific = Enumerable.Range(0, platformSpecificCount)
                                                     .Select(i =>
                                                             {
                                                                 var destination = i == 0
                                                                     ? $"Octopus.{flavour}.{platform}.nuspec"
                                                                     : $"{flavour}.Native{i}.dll";
                                                                 var source = ContentAddressedEntryName(flavour, platform, i, destination);
                                                                 entryContents[source] = Payload(source, payloadBytes);
                                                                 return new IConsolidatedPackageIndex.FileTransfer(source, destination);
                                                             });

                    platformFiles[platform] = platformSpecific.Concat(sharedEntries).ToArray();
                }

                packages[flavour] = new IConsolidatedPackageIndex.Package(flavour, "2026.2.1", IsNupkg: true, platformFiles);
            }

            var index = new ConsolidatedPackageIndex(packages);
            WriteZip(destinationPath, entryContents, index);

            var indexEntryCount = packages.Values.Sum(p => p.PlatformFiles.Values.Sum(f => f.Length));
            return new BuiltPackage(destinationPath, entryContents.Count, indexEntryCount, new FileInfo(destinationPath).Length);
        }

        static void WriteZip(string destinationPath, Dictionary<string, byte[]> entryContents, ConsolidatedPackageIndex index)
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            using var zip = ZipFile.Open(destinationPath, ZipArchiveMode.Create);

            foreach (var (entryName, content) in entryContents)
            {
                // CompressionLevel.Fastest matches what ConsolidatedPackageCreator uses.
                var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }

            var indexEntry = zip.CreateEntry("index.json", CompressionLevel.Fastest);
            using (var indexStream = indexEntry.Open())
            {
                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(index, Formatting.Indented));
                indexStream.Write(bytes, 0, bytes.Length);
            }
        }

        static string ContentAddressedEntryName(string flavour, string scope, int index, string fileName)
        {
            // Real entries are prefixed with an MD5 of the file content. Any stable, unique, hash-shaped
            // prefix reproduces the entry-name length and cardinality that the lookup has to cope with.
            var seed = $"{flavour}|{scope}|{index}";
            var hash = ((uint)seed.GetHashCode()).ToString("x8");
            return $"{hash}{hash.GetHashCode():x8}{index:x8}{scope.Length:x8}/{fileName}";
        }

        static byte[] Payload(string seed, int payloadBytes)
        {
            // Pseudo-random but deterministic, and deliberately not compressible to a constant: real
            // assemblies do not deflate to nothing, and an all-zero payload would understate inflate cost.
            var buffer = new byte[payloadBytes];
            var random = new Random(seed.GetHashCode());
            random.NextBytes(buffer);
            return buffer;
        }

        public sealed record BuiltPackage(string Path, int ArchiveEntryCount, int IndexEntryCount, long SizeOnDiskBytes);
    }
}
