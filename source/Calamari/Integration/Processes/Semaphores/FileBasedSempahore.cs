using System;
using System.Diagnostics;
using System.Threading;

namespace Calamari.Integration.Processes.Semaphores
{
    //Originally based on https://github.com/markedup-mobi/file-lock (MIT license)
    public class FileBasedSempahore : ISemaphore
    {
        public IDisposable Acquire(string name, string waitMessage)
        {
            Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Getting file based semaphore '{name}'");
            var semaphore = LockFileBasedSemaphore.Create(name, TimeSpan.FromMinutes(2));

            if (!semaphore.WaitOne(3000))
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - waiting for file based semaphore '{name}'");

                Log.Verbose(waitMessage);
                semaphore.WaitOne();
            }
            Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Got file based semaphore \'{name}\'");

            return new LockFileBasedSemaphoreReleaser(semaphore);
        }

        private class LockFileBasedSemaphoreReleaser : IDisposable
        {
            private readonly LockFileBasedSemaphore semaphore;

            public LockFileBasedSemaphoreReleaser(LockFileBasedSemaphore semaphore)
            {
                this.semaphore = semaphore;
            }

            public void Dispose()
            {
                Console.WriteLine($"{Process.GetCurrentProcess().Id}/{Thread.CurrentThread.ManagedThreadId} - Releasing file based semaphore \'{semaphore.Name}\'");
                semaphore.ReleaseLock();
            }
        }
    }
}