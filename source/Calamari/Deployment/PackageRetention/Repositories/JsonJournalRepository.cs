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
using Newtonsoft.Json.Serialization;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JsonJournalRepository : IJournalRepository
    {
        Dictionary<PackageIdentity, JournalEntry> journalEntries;
        PackageCache cache;

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
           if (journalEntries.ContainsKey(entry.Package)) //This shouldn't happen - if it already exists, then we should have just added to that entry.
            
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
                                 ?.JournalEntries
                                 ?.ToDictionary(entry => entry.Package, entry => entry)
                                 ?? new Dictionary<PackageIdentity, JournalEntry>();
                cache = packageData?.Cache ?? new PackageCache(0);
            }
            else
            {
                journalEntries = new Dictionary<PackageIdentity, JournalEntry>();
            }
        }

        void Save()
        {
            var onlyJournalEntries = journalEntries.Select(p => p.Value);
            var json = JsonConvert.SerializeObject(new PackageData(onlyJournalEntries, cache));

            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(journalPath));
            //save to temp file first
            var tempFilePath = $"{journalPath}.temp.{Guid.NewGuid()}.json";

            fileSystem.WriteAllText(tempFilePath, json, Encoding.Default);
            fileSystem.OverwriteAndDelete(journalPath, tempFilePath);
        }

        public void Dispose()
        {
            semaphore.Dispose();
        }

        class PackageData
        {
            public IEnumerable<JournalEntry> JournalEntries { get; }
            public PackageCache Cache { get;  }

            [JsonConstructor]
            public PackageData(IEnumerable<JournalEntry> journalEntries, PackageCache packageCache)
            {
                JournalEntries = journalEntries;
                Cache = packageCache;
            }
        }
    }

    public class PackageCache
    {
        [JsonProperty]
        public int CacheAge { get; set; }

        [JsonConstructor]
        public PackageCache(int cacheAge)
        {
            CacheAge = cacheAge;
        }
    }
}