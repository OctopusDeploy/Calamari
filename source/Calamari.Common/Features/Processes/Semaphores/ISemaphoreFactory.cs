using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface ISemaphoreFactory
    {
        /// <summary>
        /// Acquires a machine-wide named lock.
        /// </summary>
        IDisposable Acquire(string name, string waitMessage);
    }
}