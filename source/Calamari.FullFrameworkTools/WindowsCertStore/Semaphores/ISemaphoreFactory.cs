using System;

namespace Calamari.FullFrameworkTools.WindowsCertStore.Semaphores
{
    public interface ISemaphoreFactory
    {
        IDisposable Acquire(string name, string waitMessage);
    }
}