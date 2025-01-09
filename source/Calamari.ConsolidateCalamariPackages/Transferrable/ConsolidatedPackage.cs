using System;
using System.IO;
using SharpCompress.Archives.Zip;

namespace Calamari.ConsolidateCalamariPackages.Transferrable
{
    public class ConsolidatedPackage 
    {
        public ConsolidatedPackageIndex Index { get; init; }
        readonly string archivePath;
        
        ConsolidatedPackage(string archivePath, ConsolidatedPackageIndex index)
        {
            this.archivePath = archivePath;
            Index = index;
        }

        public static ConsolidatedPackage Create(string zipFilePath)
        {
            var loader = new ConsolidatedPackageIndexLoader();
            var index = loader.FromZipFile(zipFilePath);
            return new ConsolidatedPackage(zipFilePath, index);
        }

        public void PopulateArchive(CalamariPackage calamariPackage, Action<string, long, Stream> write)
        {
            var entry = Index.GetEntryFromIndex(calamariPackage.Flavour.Id);
            var platform = calamariPackage.Id.Substring(calamariPackage.Flavour.Id.Length).Trim('.');

            if (!entry.PlatformHashes.TryGetValue(platform, out var hashes))
            {
                throw new Exception($"Could not find platform {platform} for {calamariPackage.Flavour.Id}");
            }

            using var sourceStream = File.OpenRead(archivePath);
            using var source = ZipArchive.Open(sourceStream);
            foreach (var hash in hashes)
            {
                foreach (var sourceEntry in source.Entries)
                {
                    if (sourceEntry.Key == null || !sourceEntry.Key.StartsWith(hash)) continue;

                    using (var sourceEntryStream = sourceEntry.OpenEntryStream())
                    {
                        write(sourceEntry.Key.Substring(hash.Length + 1), sourceEntry.Size, sourceEntryStream);
                    }
                }
            }
        }
    }
}
