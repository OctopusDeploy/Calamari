using System;
using System.IO;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JsonJournalRepositoryFactory : IJournalRepositoryFactory
    {
        const string DefaultJournalName = "PackageRetentionJournal.json";

        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphoreFactory semaphoreFactory;
        readonly string journalPath;

        public JsonJournalRepositoryFactory(ICalamariFileSystem fileSystem, ISemaphoreFactory semaphoreFactory, IVariables variables)
        {
            this.fileSystem = fileSystem;
            this.semaphoreFactory = semaphoreFactory;

            var packageRetentionJournalPath = variables.Get(KnownVariables.Calamari.PackageRetentionJournalPath);
            if (packageRetentionJournalPath == null)
            {
                var tentacleHome = variables.Get(TentacleVariables.Agent.TentacleHome) ?? string.Empty; // Retention is only enabled when TentacleHome exists
                packageRetentionJournalPath = Path.Combine(tentacleHome, DefaultJournalName);
            }
            journalPath = packageRetentionJournalPath;
        }

        public IJournalRepository CreateJournalRepository()
        {
            return new JsonJournalRepository(fileSystem, semaphoreFactory, journalPath);
        }
    }
}