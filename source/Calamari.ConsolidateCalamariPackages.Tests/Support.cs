using System;
using System.IO;
using System.Linq;
using Calamari.ConsolidatedCalamariCommon;
using Newtonsoft.Json;
using SharpCompress.Archives.Zip;

namespace Calamari.ConsolidateCalamariPackages.Tests
{
    public class Support
    {
        public static ConsolidatedPackageIndex LoadIndex(string filename)
        {
            using (var zipStream = File.OpenRead(filename))
            using (var zip = ZipArchive.Open(zipStream))
            {
                var entry = zip.Entries.First(e => e.Key == "index.json");
                if (entry == null)
                {
                    throw new Exception($"index.json not found in {filename}");
                }

                using (var entryStream = entry.OpenEntryStream())
                using (var sr = new StreamReader(entryStream))
                {
#pragma warning disable CS8603 // Possible null reference return
                    return JsonConvert.DeserializeObject<ConsolidatedPackageIndex>(sr.ReadToEnd());
#pragma warning restore CS8603
                }
            }
        }
    }
}
