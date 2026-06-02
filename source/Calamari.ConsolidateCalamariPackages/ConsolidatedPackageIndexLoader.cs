using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json;
using Octopus.Calamari.ConsolidatedPackage.Api;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public class ConsolidatedPackageIndexLoader
    {
        public IConsolidatedPackageIndex Load(Stream zipStream)
        {
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
            var entry = zip.GetEntry("index.json");
            if (entry == null)
            {
                throw new Exception($"index.json not found in supplied stream.");
            }

            using var entryStream = entry.Open();
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
