using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention;
using Calamari.Deployment.PackageRetention.Model;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Tests.Fixtures.PackageRetention.Repository
{
    public class InMemoryJournalRepository : JournalRepositoryBase
    {
        public InMemoryJournalRepository(Dictionary<PackageIdentity, JournalEntry> journalEntries)
        {
            this.journalEntries = journalEntries;
            Cache = new PackageCache(0);
        }

        public InMemoryJournalRepository() : this(new Dictionary<PackageIdentity, JournalEntry>())
        {
        }

        public override void Commit()
        {
            //This does nothing in the in-memory implementation
        }

        public override void Dispose()
        {
            //This does nothing in the in-memory implementation
        }
    }
}