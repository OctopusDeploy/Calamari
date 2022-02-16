using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public abstract class JournalRepositoryBase : IJournalRepository
    {
        protected Dictionary<PackageIdentity, JournalEntry> journalEntries;
        public PackageCache Cache { get; protected set; }

        protected JournalRepositoryBase(Dictionary<PackageIdentity, JournalEntry> journalEntries = null)
        {
            this.journalEntries = journalEntries ?? new Dictionary<PackageIdentity, JournalEntry>();
        }

        public bool TryGetJournalEntry(PackageIdentity package, out JournalEntry entry)
        {
            return journalEntries.TryGetValue(package, out entry);
        }

        public void RemoveAllLocks(ServerTaskId serverTaskId)
        {
            foreach (var entry in journalEntries.Values)
                entry.RemoveLock(serverTaskId);
        }

        public JournalEntry GetJournalEntry(PackageIdentity package)
        {
            journalEntries.TryGetValue(package, out var entry);
            return entry;
        }

        public IList<JournalEntry> GetJournalEntries(PackageId packageId)
        {
            return journalEntries.Where(pair => pair.Key.PackageId == packageId)
                                 .Select(pair => pair.Value)
                                 .ToList();
        }

        public IList<JournalEntry> GetJournalEntries(PackageId packageId, ServerTaskId deploymentTaskId)
        {
            return journalEntries.Where(pair => pair.Key.PackageId == packageId
                                                && pair.Value.GetUsageDetails().Any(d => d.DeploymentTaskId == deploymentTaskId))
                                 .Select(pair => pair.Value)
                                 .ToList();
        }

        public IList<JournalEntry> GetAllJournalEntries()
        {
            return journalEntries.Select(pair => pair.Value)
                                 .ToList();
        }

        public void AddJournalEntry(JournalEntry entry)
        {
            if (journalEntries.ContainsKey(entry.Package)) return; //This shouldn't ever happen - if it already exists, then we should have just added to that entry.

            journalEntries.Add(entry.Package, entry);
        }

        public void RemovePackageEntry(PackageIdentity packageIdentity)
        {
            journalEntries.Remove(packageIdentity);
        }

        public abstract void Commit();
    }
} 