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
        readonly Dictionary<PackageID, JournalEntry> journalEntries;

        internal JournalInMemoryRepository(Dictionary<PackageID, JournalEntry> journalEntries)
        {
            this.journalEntries = journalEntries;
        }

        public bool TryGetJournalEntry(PackageID packageID, out JournalEntry entry)
        {
            return journalEntries.TryGetValue(packageID, out entry);
        }

        public JournalEntry GetJournalEntry(PackageID packageID)
        {
            journalEntries.TryGetValue(packageID, out var entry);
            return entry;
        }

        public void AddJournalEntry(JournalEntry entry)
        {
            journalEntries.Add(entry.PackageID, entry);
        }

        public void Commit()
        {
            //This does nothing in the in-memory implementation
        }
    }
}