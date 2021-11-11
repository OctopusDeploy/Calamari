using System;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JsonJournalRepositoryFactory : IJournalRepositoryFactory
    {
        internal const string DefaultJournalName = "PackageRetentionJournal.json";

        readonly ICalamariFileSystem fileSystem;
        readonly string journalPath;

        public JsonJournalRepositoryFactory(ICalamariFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;

            this.journalPath = @"C:\Octopus\PackageJournal.json";//journalPath;
        }

        public IJournalRepository CreateJournalRepository()
        {
            return new JsonJournalRepository(fileSystem, journalPath);
        }
    }
}