using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;

namespace Calamari.Deployment.PackageRetention.Repositories
{
    public class JournalJsonRepositoryFactory : IJournalRepositoryFactory
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphoreFactory semaphoreFactory;
        readonly string journalPath;

        public JournalJsonRepositoryFactory(ICalamariFileSystem fileSystem, ISemaphoreFactory semaphoreFactory)//, string journalPath)
        {
            this.fileSystem = fileSystem;
            this.semaphoreFactory = semaphoreFactory;
            this.journalPath = "c:\\Octopus\\journal.json"; 
        }

        public IJournalRepository CreateJournalRepository()
        {
            return new JournalJsonRepository(fileSystem, semaphoreFactory, journalPath);
        }
    }
}