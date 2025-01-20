using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.ConsolidateCalamariPackages.Api;
using SharpCompress.Archives.Zip;

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
                using (var source = ZipArchive.Open(sourceStream))
                {
                    foreach (var fileTransfer in platformFiles)
                    {
                        var sourceEntry = source.Entries.FirstOrDefault(e => e.Key is not null && e.Key.Equals(fileTransfer.Source));
                        if(sourceEntry is null) continue;
                    
                        using (var sourceEntryStream = sourceEntry.OpenEntryStream())
                        {
                            yield return (fileTransfer.Destination, sourceEntry.Size, sourceEntryStream);
                        }
                    }
                }
            }
        }
    }
}
