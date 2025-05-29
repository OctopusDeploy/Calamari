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
        const string PackageCacheRetentionStrategy = "MachinePackageCacheRetentionStrategy";
        const MachinePackageCacheRetentionStrategy DefaultMachinePackageCacheRetentionStrategy = MachinePackageCacheRetentionStrategy.Default;
        const int DefaultQuantityOfPackagesToKeep = -1;
        const int DefaultQuantityOfVersionsToKeep = 5;
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
            var packageCacheRetentionStrategyString = variables.Get(PackageCacheRetentionStrategy);
            var retentionStrategy = packageCacheRetentionStrategyString != null ? (MachinePackageCacheRetentionStrategy) Enum.Parse(typeof(MachinePackageCacheRetentionStrategy), packageCacheRetentionStrategyString) : DefaultMachinePackageCacheRetentionStrategy;
            
            if (!OctopusFeatureToggles.ConfigurablePackageCacheRetentionFeatureToggle.IsEnabled(variables) || retentionStrategy == MachinePackageCacheRetentionStrategy.Default)
            {
                return Array.Empty<PackageIdentity>();
            }
            
            var journals = journalEntries.ToArray();
            var quantityOfPackagesToKeep = variables.GetInt32(MachinePackageCacheRetentionQuantityOfPackagesToKeep) ?? DefaultQuantityOfPackagesToKeep;
            var quantityOfVersionsToKeep = variables.GetInt32(MachinePackageCacheRetentionQuantityOfVersionsToKeep) ?? DefaultQuantityOfVersionsToKeep;
            var packageUnitString = variables.Get(PackageUnit);
            var versionUnitString = variables.Get(VersionUnit);
            var packageUnit = packageUnitString != null ? (MachinePackageCacheRetentionUnit) Enum.Parse(typeof(MachinePackageCacheRetentionUnit), packageUnitString) : DefaultPackageUnit;
            var versionUnit = versionUnitString != null ? (MachinePackageCacheRetentionUnit) Enum.Parse(typeof(MachinePackageCacheRetentionUnit), versionUnitString) : DefaultVersionUnit;

            var orderedJournalEntriesByPackageId = JournalEntrySorter.MostRecentlyUsedByPackageId(journals).ToArray();
            
            var packagesToRemoveById = new List<PackageIdentity>();

            if (quantityOfPackagesToKeep == -1)
            {
                log.Verbose("Machine cache retention quantity of packages to keep is Keep All. No packages will be removed.");
            }
            // Package retention has currently only been implemented for number of items
            else if (packageUnit == MachinePackageCacheRetentionUnit.Items && quantityOfPackagesToKeep >= 0 && quantityOfPackagesToKeep < orderedJournalEntriesByPackageId.Length)
            {
                log.VerboseFormat("Cache size is greater than the maximum package quantity to keep {0}. {1} packages will be removed.", quantityOfPackagesToKeep, orderedJournalEntriesByPackageId.Length - quantityOfPackagesToKeep);

                packagesToRemoveById = orderedJournalEntriesByPackageId.Skip(quantityOfPackagesToKeep)
                                                            .SelectMany(entry => entry.Value.Select(v => v.Package)).ToList();
            }
            
            var packagesToRemoveByVersion = new List<PackageIdentity>();
            var orderedJournalEntriesByVersion = JournalEntrySorter.MostRecentlyUsedByVersion(journals).ToArray();

            // Version retention has currently only been implemented for number of items
            if (versionUnit == MachinePackageCacheRetentionUnit.Items)
            {
                foreach (var packageWithVersions in orderedJournalEntriesByVersion)
                {
                    packagesToRemoveByVersion.AddRange(packageWithVersions.Value.Skip(quantityOfVersionsToKeep));
                }
            }
            
            if (packagesToRemoveByVersion.Any())
            {
                log.VerboseFormat("Found cached packages with more versions than the maximum version quantity to keep {0}. {1} package versions will be removed.", quantityOfVersionsToKeep, packagesToRemoveByVersion.Count);
            }
            
            var packagesToRemove = packagesToRemoveById.Union(packagesToRemoveByVersion).ToArray();

            if (!packagesToRemove.Any())
            {
                log.VerboseFormat("The number of cached packages is below the configured maximum package quantity to keep {0} and version quantity to keep {1}. No packages will be removed.", quantityOfPackagesToKeep, quantityOfVersionsToKeep);
            }

            return packagesToRemove;
        }
    }
    
    public enum MachinePackageCacheRetentionUnit
    {
        Items
    }
}