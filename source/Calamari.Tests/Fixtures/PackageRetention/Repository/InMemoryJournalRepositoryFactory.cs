using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Tests.Fixtures.PackageRetention.Repository
{
    public class InMemoryJournalRepositoryFactory : IJournalRepositoryFactory
    {
        readonly Dictionary<PackageIdentity, JournalEntry> journalEntries;

        public InMemoryJournalRepositoryFactory()
        {
            journalEntries = new Dictionary<PackageIdentity, JournalEntry>();
        }

        public InMemoryJournalRepositoryFactory(Dictionary<PackageIdentity, JournalEntry> journalEntries)
        {
            this.journalEntries = journalEntries;
        }

        public IJournalRepository CreateJournalRepository()
        {
            return new InMemoryJournalRepository(journalEntries);
        }
    }
}