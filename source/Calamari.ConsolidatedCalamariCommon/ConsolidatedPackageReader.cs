using System;
using SharpCompress.Archives.Zip;

namespace Calamari.ConsolidatedCalamariCommon
{
    public class ConsolidatedPackageReader
    {
        public IEnumerable<(string destinationEntry, long size, Stream entryStream)> GetPackageFiles(string flavour, string platform, ConsolidatedPackageIndex index, Stream sourceStream)
        {
            var entry = index.GetEntryFromIndex(flavour);
            
            if (!entry.PlatformHashes.TryGetValue(platform, out var hashes))
            {
                throw new Exception($"Could not find platform {platform} for {flavour}");
            }
            
            using (var source = ZipArchive.Open(sourceStream))
            {
                foreach (var hash in hashes)
                {
                    foreach (var sourceEntry in source.Entries)
                    {
                        if (sourceEntry.Key == null || !sourceEntry.Key.StartsWith(hash)) continue;

                        using (var sourceEntryStream = sourceEntry.OpenEntryStream())
                        {
                            yield return (sourceEntry.Key.Substring(hash.Length + 1), sourceEntry.Size, sourceEntryStream);
                        }
                    }
                }
            }
        }
    }
}
