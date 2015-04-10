using System;
using System.Threading;

namespace Calamari.Integration.Processes
{
    public class SystemSemaphore : ISemaphore
    {
        public IDisposable Acquire(string name, string waitMessage)
        {
            var semaphore = new Semaphore(1, 1, name);
            if (!semaphore.WaitOne(1000))
            {
                Log.Verbose(waitMessage);
                semaphore.WaitOne();
            }

            return new SemaphoreReleaser(semaphore);
        }

        class SemaphoreReleaser : IDisposable
        {
            readonly Semaphore semaphore;

            public SemaphoreReleaser(Semaphore semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                semaphore.Release();
                semaphore.Dispose();
            }
        }
    }
}