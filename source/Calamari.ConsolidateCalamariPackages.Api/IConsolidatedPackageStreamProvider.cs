using System;

namespace Calamari.ConsolidateCalamariPackages.Api
{
    public interface IConsolidatedPackageStreamProvider
    {
        Stream OpenStream();
    }
}
