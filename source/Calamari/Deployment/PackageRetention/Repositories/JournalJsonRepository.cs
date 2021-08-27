using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Deployment.PackageRetention.Model;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JournalJsonRepository //: IDisposable
        : IJournalRepository
    {
        Dictionary<PackageIdentity, JournalEntry> journalEntries;
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
            Save();
        }

        //TODO: how to handle concurrency? For now we can just use JSON, but we may have to use something else.
        void Load()
        {
            if (File.Exists(journalPath))
            {
                var json = File.ReadAllText(journalPath);
                journalEntries = JsonConvert.DeserializeObject<List<JournalEntry>>(json)
                                            .ToDictionary(entry => entry.Package, entry => entry);
            }
            else
            {
                journalEntries = new  Dictionary<PackageIdentity, JournalEntry>();
            }
        }

        void Save()
        {
            using (semaphoreFactory.Acquire(SemaphoreName, "Another process is using the package retention journal"))
            {
                var journalEntryList = journalEntries.Select(p => p.Value);
                var json = JsonConvert.SerializeObject(journalEntryList);
                fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(journalPath));

                //save to temp file first
                var tempFilePath = $"{journalPath}.temp.{Guid.NewGuid()}.json";

                fileSystem.WriteAllText(tempFilePath,json, Encoding.Default);
                fileSystem.OverwriteAndDelete(journalPath, tempFilePath);
            }
        }
    }
}