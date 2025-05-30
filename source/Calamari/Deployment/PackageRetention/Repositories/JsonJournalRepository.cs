﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.PackageRetention.Model;
using Newtonsoft.Json;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JsonJournalRepository : JournalRepositoryBase
    {
        readonly ICalamariFileSystem fileSystem;
        readonly string journalPath;
        readonly ILog log;

        public JsonJournalRepository(ICalamariFileSystem fileSystem, IJsonJournalPathProvider pathProvider, ILog log)
        {
            this.fileSystem = fileSystem;
            journalPath = pathProvider.GetJournalPath();
            this.log = log;
        }

        public override void Commit()
        {
            Save();
        }

        public override void Load()
        {
            if (File.Exists(journalPath))
            {
                var json = fileSystem.ReadFile(journalPath);

                if (TryDeserializeJournal(json, out var journalContents))
                {
                    journalEntries = journalContents
                                     ?.JournalEntries
                                     ?.ToDictionary(entry => entry.Package, entry => entry)
                                     ?? new Dictionary<PackageIdentity, JournalEntry>();
                    Cache = journalContents?.Cache ?? new PackageCache(0);
                }
                else
                {
                    var journalFileName = Path.GetFileNameWithoutExtension(journalPath);
                    var backupJournalFileName = $"{journalFileName}_{DateTimeOffset.UtcNow:yyyyMMddTHHmmss}.json"; // eg. PackageRetentionJournal_20210101T120000.json

                    log.Warn($"The existing package retention journal file {journalPath} could not be read. The file will be renamed to {backupJournalFileName}. A new journal will be created.");

                    // NET Framework 4.0 doesn't have File.Move(source, dest, overwrite) so we use Copy and Delete to replicate this
                    File.Copy(journalPath, Path.Combine(Path.GetDirectoryName(journalPath), backupJournalFileName), true);
                    File.Delete(journalPath);

                    journalEntries = new Dictionary<PackageIdentity, JournalEntry>();
                    Cache = new PackageCache(0);
                }
            }
            else
            {
                journalEntries = new Dictionary<PackageIdentity, JournalEntry>();
                Cache = new PackageCache(0);
            }
        }

        void Save()
        {
            var onlyJournalEntries = journalEntries.Select(p => p.Value);
            var json = JsonConvert.SerializeObject(new PackageData(onlyJournalEntries, Cache));

            fileSystem.EnsureDirectoryExists(Path.GetDirectoryName(journalPath));
            //save to temp file first
            var tempFilePath = $"{journalPath}.temp.{Guid.NewGuid()}.json";

            fileSystem.WriteAllText(tempFilePath, json, Encoding.UTF8);
            fileSystem.OverwriteAndDelete(journalPath, tempFilePath);
        }

        class PackageData
        {
            public IEnumerable<JournalEntry> JournalEntries { get; }
            public PackageCache Cache { get; }

            [JsonConstructor]
            public PackageData(IEnumerable<JournalEntry> journalEntries, PackageCache cache)
            {
                JournalEntries = journalEntries;
                Cache = cache;
            }

            public PackageData()
            {
                JournalEntries = new List<JournalEntry>();
                Cache = new PackageCache(0);
            }
        }

        bool TryDeserializeJournal(string json, out PackageData result)
        {
            try
            {
                result = JsonConvert.DeserializeObject<PackageData>(json);
                return true;
            }
            catch (Exception e)
            {
                log.Verbose($"Unable to deserialize entries in the package retention journal file. Error message: {e.ToString()}");
                result = new PackageData();
                return false;
            }
        }
    }
}