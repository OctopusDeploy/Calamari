using System;

namespace Calamari.Integration.Processes
{
    public interface ISemaphore
    {
        IDisposable Acquire(string name, string waitMessage);
    }
}
