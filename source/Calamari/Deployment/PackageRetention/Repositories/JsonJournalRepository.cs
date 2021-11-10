using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.PackageRetention.Model;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JsonJournalRepository
        : IJournalRepository
    {
        Dictionary<PackageIdentity, JournalEntry> journalEntries;
        const string SemaphoreName = "Octopus.Calamari.PackageJournal";

        readonly ICalamariFileSystem fileSystem;
        readonly string journalPath;
        readonly ILog log;

        readonly IDisposable semaphore;

        public JsonJournalRepository(ICalamariFileSystem fileSystem, ISemaphoreFactory semaphoreFactory, string journalPath, ILog log)
        {
            this.fileSystem = fileSystem;
            this.journalPath = journalPath;
            this.log = log;

            this.semaphore = semaphoreFactory.Acquire(SemaphoreName, "Another process is using the package retention journal");

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

                if (TryParseJournal(json, out var journalContents))
                {
                    journalEntries = journalContents.ToDictionary(entry => entry.Package, entry => entry);
                }
                else
                {
                    var journalFileName = Path.GetFileNameWithoutExtension(journalPath);
                    var backupJournalFileName = $"{journalFileName}_{DateTimeOffset.UtcNow:yyyyMMddTHHmmss}.json"; // eg. PackageRetentionJournal_20210101T120000.json

                    log.Warn($"The existing package retention journal file {journalPath} could not be read. The file will be renamed to {backupJournalFileName}. A new journal will be created.");

                    var backupJournalPath = Path.GetDirectoryName(journalPath) + Path.DirectorySeparatorChar + backupJournalFileName;
                    // NET Framework 4.0 doesn't have File.Move(source, dest, overwrite) so we use Copy and Delete to replicate this
                    File.Copy(journalPath, backupJournalPath, true);
                    File.Delete(journalPath);

                    journalEntries = new Dictionary<PackageIdentity, JournalEntry>();
                }
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
            semaphore.Dispose();
        }

        static bool TryParseJournal(string json, out List<JournalEntry> result)
        {
            try
            {
                result = JsonConvert.DeserializeObject<List<JournalEntry>>(json);
                return true;
            }
            catch (Exception e)
            {
                Log.Verbose($"Unable to parse the package retention journal file. Error message: {e.ToString()}");
                result = new List<JournalEntry>();
                return false;
            }
        }
    }
}