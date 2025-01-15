using System;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.ConsolidateCalamariPackages
{
    public class ConsolidatedPackageIndex
    {
        public ConsolidatedPackageIndex(Dictionary<string, Package> packages)
        {
            Packages  = new Dictionary<string, Package>(packages, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, Package> Packages { get; init;  } 
        public IEnumerable<(string package, string version)> GetPackageVersions() => Packages.Values.Select(v => (v.PackageId, v.Version));

        public Package GetEntryFromIndex(string id)
        {
            if (!Packages.TryGetValue(id, out var indexPackage))
            {
                throw new Exception($"Package {id} not found in the consolidated package");
            }

            return indexPackage;
        }

        public class Package
        {
            public Package(string packageId, string version, bool isNupkg, Dictionary<string, FileTransfer[]> platformFiles)
            {
                PackageId = packageId;
                Version = version;
                IsNupkg = isNupkg;
                PlatformFiles = new Dictionary<string, FileTransfer[]>(platformFiles, StringComparer.OrdinalIgnoreCase);
            }

            public string PackageId { get; }
            public string Version { get; }
            public bool IsNupkg { get; }
            public Dictionary<string, FileTransfer[]> PlatformFiles { get; }
        }
    }
    
    public record FileTransfer(string Source, string Destination);
}
