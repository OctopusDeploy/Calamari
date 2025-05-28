using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class PackageQuantityPackageCacheCleaner : IRetentionAlgorithm
    {
        const string MachinePackageCacheRetentionQuantityOfPackagesToKeep = nameof(MachinePackageCacheRetentionQuantityOfPackagesToKeep);
        const string MachinePackageCacheRetentionQuantityOfVersionsToKeep = nameof(MachinePackageCacheRetentionQuantityOfVersionsToKeep);
        const string PackageUnit = "MachinePackageCacheRetentionPackageUnit";
        const string VersionUnit = "MachinePackageCacheRetentionVersionUnit";
        const int DefaultQuantityOfPackagesToKeep = 0;
        const int DefaultQuantityOfVersionsToKeep = 0;
        const MachinePackageCacheRetentionUnit DefaultPackageUnit = MachinePackageCacheRetentionUnit.Items;
        const MachinePackageCacheRetentionUnit DefaultVersionUnit = MachinePackageCacheRetentionUnit.Items;
        readonly IVariables variables;
        readonly ILog log;

        public PackageQuantityPackageCacheCleaner(IVariables variables, ILog log)
        {
            this.variables = variables;
            this.log = log;
        }

        public IEnumerable<PackageIdentity> GetPackagesToRemove(IEnumerable<JournalEntry> journalEntries)
        {
            var quantityOfVersionsToKeep = variables.GetInt32(MachinePackageCacheRetentionQuantityOfVersionsToKeep) ?? DefaultQuantityOfVersionsToKeep;
            
            if (!OctopusFeatureToggles.ConfigurablePackageCacheRetentionFeatureToggle.IsEnabled(variables) || quantityOfVersionsToKeep == 0)
            {
                return Array.Empty<PackageIdentity>();
            }
            
            var journals = journalEntries.ToArray();
            var quantityOfPackagesToKeep = variables.GetInt32(MachinePackageCacheRetentionQuantityOfPackagesToKeep) ?? DefaultQuantityOfPackagesToKeep;
            var packageUnitString = variables.Get(PackageUnit);
            var versionUnitString = variables.Get(VersionUnit);
            var packageUnit = packageUnitString != null ? (MachinePackageCacheRetentionUnit) Enum.Parse(typeof(MachinePackageCacheRetentionUnit), packageUnitString) : DefaultPackageUnit;
            var versionUnit = versionUnitString != null ? (MachinePackageCacheRetentionUnit) Enum.Parse(typeof(MachinePackageCacheRetentionUnit), versionUnitString) : DefaultVersionUnit;

            var orderedJournalEntriesByPackageId = JournalEntrySorter.MostRecentlyUsedByPackageId(journals).ToArray();
            
            var packagesToRemoveById = new List<PackageIdentity>();

            // Package retention has currently only been implemented for number of items
            if (packageUnit == MachinePackageCacheRetentionUnit.Items && quantityOfPackagesToKeep > 0 && quantityOfPackagesToKeep < orderedJournalEntriesByPackageId.Length)
            {
                log.VerboseFormat("Cache size is greater than the maximum quantity to keep. {0} packages will be removed.", orderedJournalEntriesByPackageId.Length - quantityOfPackagesToKeep);

                packagesToRemoveById = orderedJournalEntriesByPackageId.Skip(quantityOfPackagesToKeep)
                                                            .SelectMany(entry => entry.Value.Select(v => v.Package)).ToList();
            }
            
            var packagesToRemoveByVersion = new List<PackageIdentity>();
            var orderedJournalEntriesByVersion = JournalEntrySorter.MostRecentlyUsedByVersion(journals).ToArray();

            // Version retention has currently only been implemented for number of items
            if (versionUnit == MachinePackageCacheRetentionUnit.Items && quantityOfVersionsToKeep > 0)
            {
                foreach (var packageWithVersions in orderedJournalEntriesByVersion)
                {
                    packagesToRemoveByVersion.AddRange(packageWithVersions.Value.Skip(quantityOfVersionsToKeep));
                }
            }
            
            if (packagesToRemoveByVersion.Any())
            {
                log.VerboseFormat("Found cached packages with more versions than the maximum quantity to keep. {0} package versions will be removed.", packagesToRemoveByVersion.Count);
            }
            
            var packagesToRemove = packagesToRemoveById.Union(packagesToRemoveByVersion).ToArray();

            if (!packagesToRemove.Any())
            {
                log.Verbose("The number of cached packages is below the configured maximum. No packages will be removed.");
            }

            return packagesToRemove;
        }
    }
    
    public enum MachinePackageCacheRetentionUnit
    {
        Items
    }
}