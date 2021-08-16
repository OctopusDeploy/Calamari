using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Deployment.PackageRetention.Model;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JournalJsonRepository //: IDisposable
        : IJournalRepository
    {
        Dictionary<PackageID, JournalEntry> journalEntries;
        const string SemaphoreName = "Octopus.Calamari.PackageJournal";

        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphoreFactory semaphoreFactory;
        readonly string journalPath;

        internal JournalJsonRepository(ICalamariFileSystem fileSystem, ISemaphoreFactory semaphoreFactory, string journalPath)
        {
            this.fileSystem = fileSystem;
            this.semaphoreFactory = semaphoreFactory;
            this.journalPath = journalPath;

            Load();
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
            Save();
        }

        //TODO: how to handle concurrency? For now we can just use JSON, but we may have to use something else.
        void Load()
        {
            if (File.Exists(journalPath))
            {
                var json = File.ReadAllText(journalPath);
                journalEntries = JsonConvert.DeserializeObject<List<JournalEntry>>(json)
                                            .ToDictionary(entry => entry.PackageID, entry => entry);
            }
            else
            {
                journalEntries = new  Dictionary<PackageID, JournalEntry>();
            }
        }

        void Save()
        {
            using (semaphoreFactory.Acquire(SemaphoreName, "Another process is using the package retention journal"))
            {
                var json = JsonConvert.SerializeObject(journalEntries);
                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(journalPath));

                //save to temp file first
                var tempFilePath = $"{journalPath}.temp.{Guid.NewGuid()}.json";

                fileSystem.WriteAllText(journalPath, tempFilePath, Encoding.Default);
                fileSystem.OverwriteAndDelete(journalPath, tempFilePath);
            }
        }
    }
}