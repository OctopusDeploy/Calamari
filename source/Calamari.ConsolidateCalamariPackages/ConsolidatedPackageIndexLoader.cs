using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Octopus.Calamari.ConsolidatedPackage.Api;
using SharpCompress.Archives.Zip;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public class ConsolidatedPackageIndexLoader
    {
        public IConsolidatedPackageIndex Load(Stream zipStream)
        {
            using var zip = ZipArchive.Open(zipStream);
            var entry = zip.Entries.First(e => e.Key == "index.json");
            if (entry == null)
            {
                throw new Exception($"index.json not found in supplied stream.");
            }

            using var entryStream = entry.OpenEntryStream();
            return From(entryStream);
        }

        IConsolidatedPackageIndex From(Stream inputStream)
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
