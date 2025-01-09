using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SharpCompress.Archives.Zip;

namespace Calamari.ConsolidateCalamariPackages.Transferrable
{
    public class ConsolidatedPackageIndexLoader
    {
        public ConsolidatedPackageIndex FromZipFile(string zipFilepath)
        {
            using var zipStream = File.OpenRead(zipFilepath);
            using var zip = ZipArchive.Open(zipStream);
            var entry = zip.Entries.First(e => e.Key == "index.json");
            if (entry == null)
            {
                throw new Exception($"index.json not found in {zipFilepath}");
            }

            using var entryStream = entry.OpenEntryStream();
            return From(entryStream);
        }

        public ConsolidatedPackageIndex From(Stream inputStream)
        {
            using (var sr = new StreamReader(inputStream))
            {
#pragma warning disable CS8603 // Possible null reference return
                return JsonConvert.DeserializeObject<ConsolidatedPackageIndex>(sr.ReadToEnd());
#pragma warning restore CS8603
            }
        }
    }
}
