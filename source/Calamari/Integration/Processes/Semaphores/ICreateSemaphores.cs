using System;

namespace Calamari.Integration.Processes.Semaphores
{
    public interface ICreateSemaphores 
    {
        ISemaphore Create(string name, TimeSpan lockTimeout);
    }
}