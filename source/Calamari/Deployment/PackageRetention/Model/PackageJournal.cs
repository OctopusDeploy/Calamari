using System;
using System.Linq;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.Deployment.PackageRetention;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.PackageRetention.Caching;
using Calamari.Deployment.PackageRetention.Repositories;

namespace Calamari.Deployment.PackageRetention.Model
{
    public class PackageJournal
    {
        readonly IJournalRepository journalRepository;
        readonly IRetentionAlgorithm retentionAlgorithm;
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ISemaphoreFactory semaphoreFactory;

        public PackageJournal(IJournalRepository journalRepository,
                              ILog log,
                              ICalamariFileSystem fileSystem,
                              IRetentionAlgorithm retentionAlgorithm,
                              ISemaphoreFactory semaphoreFactory)
        {
            this.journalRepository = journalRepository;
            this.log = log;
            this.fileSystem = fileSystem;
            this.retentionAlgorithm = retentionAlgorithm;
            this.semaphoreFactory = semaphoreFactory;
        }

    }
}