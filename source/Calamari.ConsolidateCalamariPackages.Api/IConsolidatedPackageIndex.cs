using System;

namespace Calamari.ConsolidateCalamariPackages.Api
{
    public interface IConsolidatedPackageIndex
    {
        Package GetPackage(string id);

        public IEnumerable<(string package, string version)> GetAvailablePackages();

        public record Package(string PackageId, string Version, bool IsNupkg, Dictionary<string, FileTransfer[]> PlatformFiles);
        
        public record FileTransfer(string Source, string Destination);
    }
}
