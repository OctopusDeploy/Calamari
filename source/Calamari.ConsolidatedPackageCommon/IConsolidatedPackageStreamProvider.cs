using System.IO;

namespace Octopus.Server.Orchestration.Targets.Common.BundledPackages.Transferrable
{
    public interface IConsolidatedPackageStreamProvider
    {
        Stream OpenStream();
    }
}
