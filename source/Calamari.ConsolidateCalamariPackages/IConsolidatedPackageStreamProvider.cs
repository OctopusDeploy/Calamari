using System;
using System.IO;

namespace Calamari.ConsolidateCalamariPackages
{
    public interface IConsolidatedPackageStreamProvider
    {
        Stream OpenStream();
    }
}
