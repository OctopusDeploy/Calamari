using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Deployment.PackageRetention.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpCompress.Common;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JournalInMemoryRepository : IJournalRepository
    {
        readonly Dictionary<PackageIdentity, JournalEntry> journalEntries;

        internal JournalInMemoryRepository(Dictionary<PackageIdentity, JournalEntry> journalEntries)
        {
            this.journalEntries = journalEntries;
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

        public void Commit()
        {
            //This does nothing in the in-memory implementation
        }
    }
}