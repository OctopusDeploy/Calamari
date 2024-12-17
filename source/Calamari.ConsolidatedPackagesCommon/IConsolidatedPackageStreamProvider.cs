using System.IO;

namespace Calamari.ConsolidatedPackagesCommon
{
    public interface IConsolidatedPackageStreamProvider
    {
        Stream OpenStream();
    }
}
