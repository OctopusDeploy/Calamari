using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface ISemaphoreFactory
    {
        /// <summary>
        /// Acquires a machine-wide lock, backed by a Mutex.  This will spawn a thread
        /// to ensure the mutex is disposed on the same thread that created it.
        /// </summary>
        IDisposable Acquire(string name, string waitMessage);
    }
}