using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Deployment.PackageRetention.Model;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JsonJournalRepository
        : IJournalRepository
    {
        static readonly SemaphoreSlim JournalSemaphore = new SemaphoreSlim(1, 1);

        Dictionary<PackageIdentity, JournalEntry> journalEntries;

        readonly ICalamariFileSystem fileSystem;
        readonly string journalPath;

        public JsonJournalRepository(ICalamariFileSystem fileSystem, string journalPath)
        {
            JournalSemaphore.Wait();

            this.fileSystem = fileSystem;
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
            var journalEntryList = journalEntries.Select(p => p.Value);
            var json = JsonConvert.SerializeObject(journalEntryList);
            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(journalPath));

            //save to temp file first
            var tempFilePath = $"{journalPath}.temp.{Guid.NewGuid()}.json";

            fileSystem.WriteAllText(tempFilePath,json, Encoding.Default);
            fileSystem.OverwriteAndDelete(journalPath, tempFilePath);
        }

        public void Dispose()
        {
            JournalSemaphore.Release();
        }
    }
}