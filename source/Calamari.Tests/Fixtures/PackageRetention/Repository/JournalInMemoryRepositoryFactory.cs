using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Tests.Fixtures.PackageRetention.Repository
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