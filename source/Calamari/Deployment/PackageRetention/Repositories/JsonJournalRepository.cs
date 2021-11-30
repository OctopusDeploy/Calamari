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
    public class JsonJournalRepository : JournalRepositoryBase
    {
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

        public override void Commit()
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
                Cache = packageData?.Cache ?? new PackageCache(0);
            }
            else
            {
                journalEntries = new Dictionary<PackageIdentity, JournalEntry>();
            }
        }

        void Save()
        {
            var onlyJournalEntries = journalEntries.Select(p => p.Value);
            var json = JsonConvert.SerializeObject(new PackageData(onlyJournalEntries, Cache));

            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(journalPath));
            //save to temp file first
            var tempFilePath = $"{journalPath}.temp.{Guid.NewGuid()}.json";

            fileSystem.WriteAllText(tempFilePath, json, Encoding.Default);
            fileSystem.OverwriteAndDelete(journalPath, tempFilePath);
        }

        public override void Dispose()
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
}