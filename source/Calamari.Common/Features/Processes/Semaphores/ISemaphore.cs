using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface ISemaphore
    {
        string Name { get; }
        void ReleaseLock();
        bool WaitOne();
        bool WaitOne(int millisecondsToWait);
    }
}