using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ConsolidateCalamariPackages.Api;

namespace Octopus.Calamari.ConsolidatedPackage
{
    public class ConsolidatedPackageIndex : IConsolidatedPackageIndex
    {
        public ConsolidatedPackageIndex(Dictionary<string, IConsolidatedPackageIndex.Package> packages)
        {
            Packages  = new Dictionary<string, IConsolidatedPackageIndex.Package>(packages, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, IConsolidatedPackageIndex.Package> Packages { get; init; }

        public IConsolidatedPackageIndex.Package GetPackage(string id)
        {
            if (!Packages.TryGetValue(id, out var indexPackage))
            {
                throw new Exception($"Package {id} not found in the consolidated package");
            }

            return indexPackage;
        }
        
        public IEnumerable<(string package, string version)> GetAvailablePackages()
        {
            return Packages.Values.Select(v => (v.PackageId, v.Version));
        }
    }
}
