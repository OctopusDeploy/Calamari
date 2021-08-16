using System.Collections.Generic;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JournalInMemoryRepositoryFactory : IJournalRepositoryFactory
    {
        readonly Dictionary<PackageID, JournalEntry> journalEntries;

        public JournalInMemoryRepositoryFactory()
        {
            journalEntries = new Dictionary<PackageID, JournalEntry>();
        }

        public IJournalRepository CreateJournalRepository()
        {
            return new JournalInMemoryRepository(journalEntries);
        }
    }
}