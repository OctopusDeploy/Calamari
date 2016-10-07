using System;

namespace Calamari.Integration.Processes.Semaphores
{
    public interface ISemaphore
    {
        IDisposable Acquire(string name, string waitMessage);
    }
}
