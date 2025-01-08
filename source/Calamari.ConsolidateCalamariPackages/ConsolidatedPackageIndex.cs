using System;
using System.Collections.Generic;

namespace Calamari.ConsolidateCalamariPackages
{
    public class ConsolidatedPackageIndex
    {
        public ConsolidatedPackageIndex(Dictionary<string, Package> packages)
        {
            Packages = new Dictionary<string, Package>(packages, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, Package> Packages { get; }

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
    
    public record FileTransfer(String Source, string Destination);
}