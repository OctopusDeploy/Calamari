using System;
using SharpCompress.Archives.Zip;

namespace Calamari.ConsolidatedPackagesCommon
{
    public interface IConsolidatedPackage
    {
        public IEnumerable<(string destinationEntry, long size, Stream sourceStream)> ExtractCalamariPackage(string calamariFlavour, string platform);

        public IEnumerable<(string package, string version)> GetAvailablePackages();

        public ConsolidatedPackageIndex.Package GetPackage(string calamariFlavour);

    }
    
    public class ConsolidatedPackage : IConsolidatedPackage
    {
        readonly ConsolidatedPackageIndex index;
        readonly IConsolidatedPackageStreamProvider packageStreamProvider;
        
        public ConsolidatedPackage(IConsolidatedPackageStreamProvider packageStreamProvider, ConsolidatedPackageIndex index)
        {
            this.packageStreamProvider = packageStreamProvider;
            this.index = index;
        }
        
        public IEnumerable<(string package, string version)> GetAvailablePackages()
        {
            return index.Packages.Values.Select(v => (v.PackageId, v.Version));
        }

        public ConsolidatedPackageIndex.Package GetPackage(string calamariFlavour) => index.GetEntryFromIndex(calamariFlavour);

        public IEnumerable<(string destinationEntry, long size, Stream sourceStream)> ExtractCalamariPackage(string calamariFlavour, string platform)
        {
            var entry = index.GetEntryFromIndex(calamariFlavour);

            if (!entry.PlatformHashes.TryGetValue(platform, out var hashes))
            {
                throw new Exception($"Could not find platform {platform} for {calamariFlavour}");
            }

            using (var sourceStream = packageStreamProvider.OpenStream())
            {
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
}
