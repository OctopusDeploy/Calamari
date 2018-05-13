using System;

namespace Calamari.Integration.Processes.Semaphores
{
    public interface ISemaphoreFactory
    {
        IDisposable Acquire(string name, string waitMessage);
    }
}
