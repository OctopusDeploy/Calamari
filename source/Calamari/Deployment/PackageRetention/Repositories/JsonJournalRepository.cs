using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Model;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JsonJournalRepository
        : IJournalRepository
    {
        Dictionary<PackageIdentity, JournalEntry> journalEntries;
        const string SemaphoreName = "Octopus.Calamari.PackageJournal";
        internal const string DefaultJournalName = "PackageRetentionJournal.json";

        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphoreFactory semaphoreFactory;
        readonly string journalPath;
        readonly ILog log;

        public JsonJournalRepository(ICalamariFileSystem fileSystem, ISemaphoreFactory semaphoreFactory, IVariables variables, ILog log)
        {
            this.fileSystem = fileSystem;
            this.semaphoreFactory = semaphoreFactory;
            this.log = log;

            var packageRetentionJournalPath = variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath);
            if (packageRetentionJournalPath == null)
            {
                var tentacleHome = variables.Get(TentacleVariables.Agent.TentacleHome) ?? throw new Exception("Environment variable 'TentacleHome' has not been set.");
                packageRetentionJournalPath = Path.Combine(tentacleHome, DefaultJournalName);
            }
            journalPath = packageRetentionJournalPath;

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

        //TODO: Handle concurrency. We should be able to use a semaphore for this (i.e. wait/lock/release), otherwise we may need to use something else.
        //We are always just opening the file, adding to it, then saving it in pretty much one atomic step, so a semaphore should work ok. See Journal.RegisterPackageUse for an example.
        //We will need to use the semaphore across the load/save though, which needs to be worked out.  Maybe make repositories disposable and have the semaphore held until dispose?
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
                    var backupJournalPath = $"{journalFileName}_{DateTimeOffset.UtcNow:yyyyMMddTHHmmss}.json"; // eg. PackageRetentionJournal_20210101T120000.json

                    log.Warn($"The existing package retention journal file {journalPath} could not be read. The file will be renamed to {backupJournalPath}. A new journal will be created.");
                    // NET Framework 4.0 doesn't have FIle.Move(source, dest, overwrite) so we use Copy and Delete to replicate this
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