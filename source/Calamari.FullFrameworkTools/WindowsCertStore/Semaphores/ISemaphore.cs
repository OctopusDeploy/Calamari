using System;

namespace Calamari.FullFrameworkTools.WindowsCertStore.Semaphores
{
    public interface ISemaphore
    {
        string Name { get; }
        void ReleaseLock();
        bool WaitOne();
        bool WaitOne(int millisecondsToWait);
    }
}