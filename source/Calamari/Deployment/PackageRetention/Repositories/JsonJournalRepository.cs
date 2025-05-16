using System;
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



 


        public class PackageData
        {
            public List<JournalEntry> JournalEntries { get; }
            public PackageCache Cache { get; }

            [JsonConstructor]
            public PackageData(List<JournalEntry> journalEntries, PackageCache cache)
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

        static bool TryDeserializeJournal(string json, out PackageData result)
        {
            try
            {
                result = JsonConvert.DeserializeObject<PackageData>(json);
                return true;
            }
            catch (Exception e)
            {
                Log.Verbose($"Unable to deserialize entries in the package retention journal file. Error message: {e.ToString()}");
                result = new PackageData();
                return false;
            }
        }
    }
}