using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Deployment.PackageRetention.Model;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JsonJournalRepository : IJournalRepository
    {

        Dictionary<PackageIdentity, JournalEntry> journalEntries;
        PackageCache cache = new PackageCache();

        const string SemaphoreName = "Octopus.Calamari.PackageJournal";

        readonly ICalamariFileSystem fileSystem;
        readonly string journalPath;
        readonly IDisposable semaphore;

        public JsonJournalRepository(ICalamariFileSystem fileSystem, ISemaphoreFactory semaphoreFactory, string journalPath)
        {
            this.fileSystem = fileSystem;
            this.journalPath = journalPath;
            semaphore = semaphoreFactory.Acquire(SemaphoreName, "Another process is using the package retention journal");

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

        public IList<JournalEntry> GetJournalEntries(PackageId packageId)
        {
            return journalEntries.Where(pair => pair.Key.PackageId == packageId)
                                 .Select(pair => pair.Value)
                                 .ToList();
        }

        public IList<JournalEntry> GetAllJournalEntries()
        {
            return journalEntries.Select(pair => pair.Value)
                                 .ToList();
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
                var packageData = JsonConvert.DeserializeObject<PackageData>(json);

                journalEntries = packageData
                                 .JournalEntries
                                 .ToDictionary(entry => entry.Package, entry => entry);
            }
            else
            {
                journalEntries = new Dictionary<PackageIdentity, JournalEntry>();
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
            semaphore.Dispose();
        }

        class PackageData
        {
            public List<JournalEntry> JournalEntries { get; }
            public PackageCache Cache { get; }

            public PackageData(List<JournalEntry> journalEntries, PackageCache cache)
            {
                JournalEntries = journalEntries;
                Cache = cache;
            }
        }
    }

    public class PackageCache
    {
        public int CacheAge { get; set; }
    }
}