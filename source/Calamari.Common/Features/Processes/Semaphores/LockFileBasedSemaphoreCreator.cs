using System;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public class LockFileBasedSemaphoreCreator : ICreateSemaphores
    {
        readonly ILog log;

        public LockFileBasedSemaphoreCreator(ILog log)
        {
            this.log = log;
        }

        public ISemaphore Create(string name, TimeSpan lockTimeout)
        {
            return new LockFileBasedSemaphore(name, lockTimeout, log);
        }
    }
}