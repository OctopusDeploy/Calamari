using System;

namespace Calamari.Common.Features.Processes.NamedLocks
{
    public interface INamedLockManager
    {
        IDisposable Acquire(string name, string waitMessage);
    }
}