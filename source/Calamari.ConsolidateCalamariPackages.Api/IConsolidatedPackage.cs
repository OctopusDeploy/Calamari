using System;

namespace Calamari.ConsolidateCalamariPackages.Api
{
    public interface IConsolidatedPackage
    {
        IConsolidatedPackageIndex Index { get; }
        IEnumerable<(string entryName, long size, Stream sourceStream)> ExtractCalamariPackage(string calamariFlavour, string platform);
        
    }
}
