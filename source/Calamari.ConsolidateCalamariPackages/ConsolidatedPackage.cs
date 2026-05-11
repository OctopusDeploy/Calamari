using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Octopus.Calamari.ConsolidatedPackage.Api;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public class ConsolidatedPackage : IConsolidatedPackage
    {
        readonly IConsolidatedPackageStreamProvider packageStreamProvider;

        public ConsolidatedPackage(IConsolidatedPackageStreamProvider packageStreamProvider, IConsolidatedPackageIndex index)
        {
            this.packageStreamProvider = packageStreamProvider;
            Index = index;
        }

        public IConsolidatedPackageIndex Index { get; }

        public IEnumerable<(string entryName, long size, Stream sourceStream)> ExtractCalamariPackage(string calamariFlavour, string platform)
        {
            var entry = Index.GetPackage(calamariFlavour);

            if (!entry.PlatformFiles.TryGetValue(platform, out var platformFiles))
            {
                throw new Exception($"Could not find platform {platform} for {calamariFlavour}");
            }

            using (var sourceStream = packageStreamProvider.OpenStream())
            {
                using (var source = new ZipArchive(sourceStream, ZipArchiveMode.Read))
                {
                    foreach (var fileTransfer in platformFiles)
                    {
                        var sourceEntry = source.Entries.FirstOrDefault(e => e.FullName.Equals(fileTransfer.Source));
                        if (sourceEntry is null) continue;

                        using (var sourceEntryStream = sourceEntry.Open())
                        {
                            yield return (fileTransfer.Destination, sourceEntry.Length, sourceEntryStream);
                        }
                    }
                }
            }
        }

        //
        // var platformFilesLookup = platformFiles.ToDictionary(f => f.Source);
        //
        // using var sourceStream = packageStreamProvider.OpenStream();
        // using var source = new ZipArchive(sourceStream, ZipArchiveMode.Read);
        // foreach (var sourceEntry in source.Entries)
        // {
        //     if (!platformFilesLookup.TryGetValue(sourceEntry.FullName, out var fileTransfer)) continue;
        //
        //     using var sourceEntryStream = sourceEntry.Open();
        //     yield return (fileTransfer.Destination, sourceEntry.Length, sourceEntryStream);
        // }
    }
}