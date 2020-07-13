using System;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public interface ICreateSemaphores
    {
        ISemaphore Create(string name, TimeSpan lockTimeout);
    }
}