using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Tests.Fixtures.PackageRetention.Repository
{
    public class InMemoryJournalRepository : IJournalRepository
    {
        readonly Dictionary<PackageIdentity, JournalEntry> journalEntries;

        public InMemoryJournalRepository(Dictionary<PackageIdentity, JournalEntry> journalEntries)
        {
            this.journalEntries = journalEntries;
        }

        public InMemoryJournalRepository() : this(new Dictionary<PackageIdentity, JournalEntry>())
        {
        }

        public bool TryGetJournalEntry(PackageIdentity package, out JournalEntry entry)
        {
            return journalEntries.TryGetValue(package, out entry);
        }

        public JournalEntry GetJournalEntry(PackageIdentity package)
        {
            journalEntries.TryGetValue(package, out var entry);
            return entry;
        }

        public void AddJournalEntry(JournalEntry entry)
        {
            journalEntries.Add(entry.Package, entry);
        }

        public IList<JournalEntry> GetAllJournalEntries()
        {
            return journalEntries.Select(pair => pair.Value)
                                 .ToList();
        }

        public void RemovePackageEntry(PackageIdentity packageIdentity)
        {
            journalEntries.Remove(packageIdentity);
        }

        public void Commit()
        {
            //This does nothing in the in-memory implementation
        }

        public void Dispose()
        {
            //This does nothing in the in-memory implementation
        }
    }
}