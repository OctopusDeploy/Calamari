using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface ISemaphoreFactory
    {
        IDisposable Acquire(string name, string waitMessage);
    }
}