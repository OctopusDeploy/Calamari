using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public interface IJournalRepository : IDisposable
    {
        bool TryGetJournalEntry(PackageIdentity package, out JournalEntry entry);
        JournalEntry GetJournalEntry(PackageIdentity packageId);
        void AddJournalEntry(JournalEntry entry);
        IList<JournalEntry> GetAllJournalEntries();
        void RemovePackageEntry(PackageIdentity packageIdentity);
        void Commit();
    }
}