using System;

namespace Calamari.Integration.Processes.Semaphores
{
    public class LockFileBasedSemaphoreCreator : ICreateSemaphores
    {
        public ISemaphore Create(string name, TimeSpan lockTimeout)
        {
            return new LockFileBasedSemaphore(name, lockTimeout);
        }
    }
}