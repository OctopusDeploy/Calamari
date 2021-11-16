using System;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JsonJournalRepositoryFactory : IJournalRepositoryFactory
    {
        internal const string DefaultJournalName = "PackageRetentionJournal.json";

        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphoreFactory semaphoreFactory;
        readonly string journalPath;

        public JsonJournalRepositoryFactory(ICalamariFileSystem fileSystem, ISemaphoreFactory semaphoreFactory)
        {
            this.fileSystem = fileSystem;
            this.semaphoreFactory = semaphoreFactory;

            this.journalPath = @"C:\Octopus\PackageJournal.json";//journalPath;
        }

        public IJournalRepository CreateJournalRepository()
        {
            return new JsonJournalRepository(fileSystem, semaphoreFactory, journalPath);
        }
    }
}