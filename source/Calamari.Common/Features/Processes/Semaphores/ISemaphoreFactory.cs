using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface ISemaphoreFactory
    {
        /// <summary>
        /// Acquires a machine-wide lock. The lock is backed by a named Mutex, so the returned
        /// <see cref="IDisposable"/> must be disposed on the same thread that called Acquire;
        /// releasing from another thread (including after an await) throws.
        /// </summary>
        IDisposable Acquire(string name, string waitMessage);
    }
}