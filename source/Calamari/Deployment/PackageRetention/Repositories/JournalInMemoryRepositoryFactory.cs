using System.Collections.Generic;
using Calamari.Deployment.PackageRetention.Model;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JournalInMemoryRepositoryFactory : IJournalRepositoryFactory
    {
        readonly Dictionary<PackageIdentity, JournalEntry> journalEntries;

        public JournalInMemoryRepositoryFactory()
        {
            journalEntries = new Dictionary<PackageIdentity, JournalEntry>();
        }

        public IJournalRepository CreateJournalRepository()
        {
            return new JournalInMemoryRepository(journalEntries);
        }
    }
}