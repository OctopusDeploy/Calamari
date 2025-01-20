using System;
using Calamari.ConsolidateCalamariPackages.Api;
using Octopus.Calamari.ConsolidatedPackage;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public class ConsolidatedPackageFactory
    {
        readonly ConsolidatedPackageIndexLoader indexLoader = new();

        public IConsolidatedPackage LoadFrom(IConsolidatedPackageStreamProvider streamProvider)
        {
            using (var stream = streamProvider.OpenStream())
            {
                var index = indexLoader.Load(stream);
                return new ConsolidatedPackage(streamProvider, index);
            }
        }
    }
}