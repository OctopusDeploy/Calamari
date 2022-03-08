using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public interface IJournalRepository
    {
        bool TryGetJournalEntry(PackageIdentity package, out JournalEntry entry);
        PackageCache Cache { get; }
        void RemoveAllLocks(ServerTaskId serverTaskId);
        IList<JournalEntry> GetAllJournalEntries();
        void AddJournalEntry(JournalEntry entry);
        void RemovePackageEntry(PackageIdentity packageIdentity);
        void Load();
        void Commit();
    }
}