using System;

namespace Calamari.ConsolidatedCalamariCommon
{
    public interface IConsolidatedPackageReader
    {
        IEnumerable<(string destinationEntry, long size, Stream entryStream)> GetPackageFiles(string flavour, string platform, ConsolidatedPackageIndex index, Stream sourceStream);
    }
}
