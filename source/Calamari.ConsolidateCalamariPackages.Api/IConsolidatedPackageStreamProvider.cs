using System;

namespace Octopus.Calamari.ConsolidatedPackage.Api
{
    public interface IConsolidatedPackageStreamProvider
    {
        Stream OpenStream();
    }
}
