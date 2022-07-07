using System;
using Calamari.Common.Plumbing;

namespace Calamari.Common.Features.Processes.Semaphores
{
    public static class SemaphoreFactory
    {
        public static ISemaphoreFactory Get()
        {
            return new SystemSemaphoreManager();
        }
    }
}