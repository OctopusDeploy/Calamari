
using System;

namespace Calamari.ConsolidateCalamariPackages
{
    public class ConsolidatedPackageFactory
    {
        readonly ConsolidatedPackageIndexLoader indexLoader = new();
        
        public ConsolidateCalamariPackages.ConsolidatedPackage LoadFrom(IConsolidatedPackageStreamProvider streamProvider)
        {
            using (var stream = streamProvider.OpenStream())
            {
                var index = indexLoader.Load(stream);
                return new ConsolidateCalamariPackages.ConsolidatedPackage(streamProvider, index);
            }
        }
    }
}
