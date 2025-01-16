using System;
using System.IO;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public interface IConsolidatedPackageStreamProvider
    {
        Stream OpenStream();
    }
}
