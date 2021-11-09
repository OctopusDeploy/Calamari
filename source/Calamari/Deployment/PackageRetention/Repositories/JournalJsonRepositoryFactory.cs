using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.PackageRetention.Variables;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JournalJsonRepositoryFactory : IJournalRepositoryFactory
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphoreFactory semaphoreFactory;
        readonly string journalPath;

        public JournalJsonRepositoryFactory(IVariables variables, ICalamariFileSystem fileSystem, ISemaphoreFactory semaphoreFactory)//, string journalPath)
        {
            this.fileSystem = fileSystem;
            this.semaphoreFactory = semaphoreFactory;
            this.journalPath = variables.Get(PackageRetentionVariables.PackageRetentionJournalPath);
        }

        public IJournalRepository CreateJournalRepository()
        {
            return new JournalJsonRepository(fileSystem, semaphoreFactory, journalPath);
        }
    }
}