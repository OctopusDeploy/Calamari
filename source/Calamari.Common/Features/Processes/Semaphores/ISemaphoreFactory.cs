using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface ISemaphoreFactory
    {
        /// <summary>
        /// Acquires a machine-wide lock. The lock may be backed by a named Mutex, which can only be waited
        /// on and released by the thread that acquired it; implementations are responsible for hiding that
        /// constraint, so the returned <see cref="IDisposable"/> is safe to dispose from any thread, including
        /// after an await.
        /// </summary>
        IDisposable Acquire(string name, string waitMessage);
    }
}