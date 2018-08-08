using System;

namespace Calamari.Shared
{
    public interface ISemaphoreFactory
    {
        IDisposable Acquire(string name, string waitMessage);
    }
}
