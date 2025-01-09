using System;

namespace Calamari.ConsolidatedCalamariCommon
{
    public class ConsolidatedPackageIndex
    {
        public ConsolidatedPackageIndex(Dictionary<string, Package> packages)
        {
            Packages = new Dictionary<string, Package>(packages, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, Package> Packages { get; }
        public IEnumerable<(string packageId, string version)> PackageVersions => Packages.Values.Select(v => (v.PackageId, v.Version));

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
            public Package(string packageId, string version, bool isNupkg, Dictionary<string, string[]> platformHashes)
            {
                PackageId = packageId;
                Version = version;
                IsNupkg = isNupkg;
                PlatformHashes = new Dictionary<string, string[]>(platformHashes, StringComparer.OrdinalIgnoreCase);
            }

            public string PackageId { get; }
            public string Version { get; }
            public bool IsNupkg { get; }
            public Dictionary<string, string[]> PlatformHashes { get; }
        }
    }
}
